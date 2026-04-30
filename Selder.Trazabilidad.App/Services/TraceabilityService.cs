// Services/TraceabilityService.cs
using System.Text;
using System.Text.Json;
using Selder.Trazabilidad.App.Models;
using SQLite;


namespace Selder.Trazabilidad.App.Services;

/// <summary>
/// Servicio centralizado de trazabilidad.
/// Etapas válidas que acepta la API: INICIO | PRODUCCION | FIN | DICTAMEN | FINALIZADO
/// </summary>
public class TraceabilityService
{
    // ── Etapas válidas (espejo del switch en TrazabilidadController) ─────────
    public static class Etapa
    {
        public const string Inicio = "INICIO";
        public const string Produccion = "PRODUCCION";
        public const string Fin = "FIN";
        public const string Dictamen = "DICTAMEN";
        public const string Finalizado = "FINALIZADO";
    }

    private readonly SQLiteAsyncConnection _db;
    private readonly string _apiUrl;

    private static readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    static TraceabilityService()
    {
        // Header requerido por ngrok — se aplica una sola vez al cliente estático
        _http.DefaultRequestHeaders.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");
    }

    public TraceabilityService(SQLiteAsyncConnection db, string apiUrl)
    {
        _db = db;
        _apiUrl = apiUrl;
    }

    // ── Método principal ─────────────────────────────────────────────────────

    /// <summary>
    /// Registra un evento de fabricación para el lote dado.
    /// <para>Usa las constantes <see cref="Etapa"/> para evitar typos.</para>
    /// </summary>
    /// <param name="etapa">INICIO | PRODUCCION | FIN | DICTAMEN | FINALIZADO</param>
    /// <param name="numLote">Número de lote (solo dígitos, igual que en la app)</param>
    /// <param name="observaciones">Campo libre, opcional</param>
    public async Task<TraceResult> LogEventAsync(string etapaApi, string numLote, string etapaParaLocal = "")
    {
        // 1. Normalizamos los datos (Sin quitar letras del lote)
        etapaApi = etapaApi.Trim().ToUpper();
        numLote = numLote.Trim().ToUpper();

        if (string.IsNullOrEmpty(numLote))
            return TraceResult.Fail("El número de lote no es válido.");

        // Definimos qué etiqueta guardar en SQLite (si no mandas etapaParaLocal, usa la de la API)
        string etiquetaGuardado = string.IsNullOrEmpty(etapaParaLocal) ? etapaApi : etapaParaLocal;

        // 2. Validar contra las etapas que acepta tu controlador API
        var etapasValidas = new[] { Etapa.Inicio, Etapa.Produccion, Etapa.Fin, Etapa.Dictamen, Etapa.Finalizado };
        if (!etapasValidas.Contains(etapaApi))
            return TraceResult.Fail($"La etapa '{etapaApi}' no es reconocida por el servidor.");

        // 3. Preparar Payload para SQL Server
        var payload = JsonSerializer.Serialize(new { numLote, etapa = etapaApi });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync(_apiUrl, content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                // Guardamos en SQLite con la etiqueta descriptiva (ej. PESADO - INICIO)
                await GuardarLocalAsync(numLote, etiquetaGuardado, "", sincronizado: true);
                return TraceResult.Ok("Registro exitoso.");
            }

            var cuerpo = await response.Content.ReadAsStringAsync();
            string mensajeApi = ExtraerMensajeApi(cuerpo) ?? $"HTTP {(int)response.StatusCode}";

            await GuardarLocalAsync(numLote, etiquetaGuardado, "", sincronizado: false);
            return TraceResult.Fail($"La API respondió: {mensajeApi}");
        }
        catch (TaskCanceledException)
        {
            await GuardarLocalAsync(numLote, etiquetaGuardado, "", sincronizado: false);
            return TraceResult.Offline("Tiempo de espera agotado. Guardado localmente.");
        }
        catch (HttpRequestException ex)
        {
            await GuardarLocalAsync(numLote, etiquetaGuardado, "", sincronizado: false);
            return TraceResult.Offline($"Sin conexión. Guardado localmente.");
        }
    }

    // ── Sincronización de pendientes ─────────────────────────────────────────

    /// <summary>
    /// Reintenta enviar todos los movimientos guardados localmente sin sincronizar.
    /// Llámalo al iniciar la app o cuando detectes que volvió la red.
    /// </summary>
    public async Task<(int enviados, int fallidos)> SyncPendientesAsync()
    {
        var pendientes = await _db.Table<MovimientoLocal>()
                                  .Where(m => !m.Sincronizado)
                                  .ToListAsync();
        int enviados = 0, fallidos = 0;

        foreach (var mov in pendientes)
        {
            var resultado = await LogEventAsync(mov.Etapa, mov.NumLote);

            if (resultado.IsSuccess)
            {
                // Marcar el registro original como sincronizado
                mov.Sincronizado = true;
                await _db.UpdateAsync(mov);
                enviados++;
            }
            else
            {
                fallidos++;
            }
        }

        return (enviados, fallidos);
    }

    // ── Helpers privados ─────────────────────────────────────────────────────

    private Task GuardarLocalAsync(string numLote, string etapa, string observaciones, bool sincronizado)
        => _db.InsertAsync(new MovimientoLocal
        {
            NumLote = numLote,
            Etapa = etapa,
            Fecha = DateTime.Now,
            Sincronizado = sincronizado
            // Si en el futuro agregas Observaciones a MovimientoLocal, añádela aquí
        });

    /// <summary>
    /// Tu API devuelve { "error": "..." } o { "mensaje": "..." }.
    /// Extraemos el valor sin depender de un modelo deserializado.
    /// </summary>
    private static string? ExtraerMensajeApi(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err)) return err.GetString();
            if (root.TryGetProperty("mensaje", out var msg)) return msg.GetString();
        }
        catch { /* JSON malformado — devolvemos null */ }

        return null;
    }
}

// ── Resultado tipado ─────────────────────────────────────────────────────────

public class TraceResult
{
    public enum TipoResultado { Success, Offline, Fail }

    public TipoResultado Tipo { get; private set; }
    public string Message { get; private set; } = "";

    public bool IsSuccess => Tipo == TipoResultado.Success;
    public bool IsOffline => Tipo == TipoResultado.Offline;
    public bool IsFail => Tipo == TipoResultado.Fail;

    public static TraceResult Ok(string msg) => new() { Tipo = TipoResultado.Success, Message = msg };
    public static TraceResult Offline(string msg) => new() { Tipo = TipoResultado.Offline, Message = msg };
    public static TraceResult Fail(string msg) => new() { Tipo = TipoResultado.Fail, Message = msg };
}
