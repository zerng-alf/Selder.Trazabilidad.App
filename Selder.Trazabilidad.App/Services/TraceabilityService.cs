using System.Text;
using System.Text.Json;
using Selder.Trazabilidad.App.Models;
using Selder.Trazabilidad.App.Configuration;
using SQLite;
using Sentry;

namespace Selder.Trazabilidad.App.Services;

public class TraceabilityService
{
    // ============================================================
    // ETAPAS VÁLIDAS - Constantes centralizadas
    // ============================================================
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
    private static readonly HttpClient _http;

    static TraceabilityService()
    {
        var handler = new HttpClientHandler();

        if (!AppConfig.ValidateSslCertificates)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(AppConfig.ApiTimeoutSeconds)
        };

        _http.DefaultRequestHeaders.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");
    }

    public TraceabilityService(SQLiteAsyncConnection db, string? apiUrl = null)
    {
        _db = db;
        _apiUrl = apiUrl ?? AppConfig.TrazabilidadEndpoint;
    }

    // ============================================================
    // MÉTODO PRINCIPAL - Registrar evento de fabricación (4 Etapas x 4 Campos)
    // ============================================================
    public async Task<TraceResult> LogEventAsync(string etapaApi, string numLote, string subEtapa, string idUsuario, string etapaParaLocal = "")
    {
        if (string.IsNullOrWhiteSpace(etapaApi) || string.IsNullOrWhiteSpace(numLote) || string.IsNullOrWhiteSpace(subEtapa))
        {
            SentrySdk.CaptureMessage($"Parametros inválidos: etapaApi='{etapaApi}', numLote='{numLote}', subEtapa='{subEtapa}'");
            return TraceResult.Fail("Parámetros de registro inválidos.");
        }

        etapaApi = etapaApi.Trim().ToUpper();
        numLote = numLote.Trim().ToUpper();
        subEtapa = subEtapa.Trim().ToUpper();

        if (string.IsNullOrEmpty(numLote))
        {
            SentrySdk.CaptureMessage($"Intento de registro con lote vacío");
            return TraceResult.Fail("El número de lote no es válido.");
        }

        string etiquetaGuardado = string.IsNullOrEmpty(etapaParaLocal) ? $"{etapaApi} - {subEtapa}" : etapaParaLocal;

        // PAYLOAD EN MINÚSCULAS PARA TU NUEVO DTO EN LA API
        var payload = JsonSerializer.Serialize(new
        {
            numLote = numLote,
            etapa = etapaApi,
            subEtapa = subEtapa,
            idUsuario = idUsuario
        });

        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            SentrySdk.CaptureMessage($"Enviando registro: Lote={numLote}, Etapa={etapaApi}, SubEtapa={subEtapa}, Usuario={idUsuario}");

            var response = await _http.PostAsync(_apiUrl, content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var cuerpoExito = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(cuerpoExito);
                var root = doc.RootElement;

                bool yaRegistrado = root.TryGetProperty("yaRegistrado", out var yr) && yr.GetBoolean();
                string fechaOriginal = root.TryGetProperty("fechaOriginal", out var fo) ? fo.GetString() : "";

                await GuardarLocalAsync(numLote, etiquetaGuardado, "", sincronizado: true);
                SentrySdk.CaptureMessage($"Registro procesado en servidor: Lote={numLote}, YaExistia={yaRegistrado}");

                if (yaRegistrado)
                    return TraceResult.Ok("YA FUE ESCANEADO ANTES", fechaOriginal, esDuplicado: true);
                else
                    return TraceResult.Ok("Registro exitoso.", fechaOriginal, esDuplicado: false);
            }

            var cuerpo = await response.Content.ReadAsStringAsync();
            string mensajeApi = ExtraerMensajeApi(cuerpo) ?? $"HTTP {(int)response.StatusCode}";

            await GuardarLocalAsync(numLote, etiquetaGuardado, "", sincronizado: false);
            SentrySdk.CaptureMessage($"Error API: {mensajeApi}");
            return TraceResult.Fail($"La API respondió: {mensajeApi}");
        }
        catch (TaskCanceledException)
        {
            await GuardarLocalAsync(numLote, etiquetaGuardado, "", sincronizado: false);
            SentrySdk.CaptureMessage($"Timeout - guardado offline: Lote={numLote}");
            return TraceResult.Offline("Tiempo de espera agotado. Guardado localmente.");
        }
        catch (HttpRequestException ex)
        {
            await GuardarLocalAsync(numLote, etiquetaGuardado, "", sincronizado: false);
            SentrySdk.CaptureException(ex);
            return TraceResult.Offline("Sin conexión. Guardado localmente.");
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            return TraceResult.Fail($"Error inesperado: {ex.Message}");
        }
    }

    // ============================================================
    // SINCRONIZACIÓN DE PENDIENTES - CORREGIDO
    // ============================================================
    public async Task<(int enviados, int fallidos)> SyncPendientesAsync()
    {
        var pendientes = await _db.Table<MovimientoLocal>()
                                  .Where(m => !m.Sincronizado)
                                  .ToListAsync();

        int enviados = 0, fallidos = 0;

        SentrySdk.CaptureMessage($"Iniciando sincronización: {pendientes.Count} registros pendientes");

        foreach (var mov in pendientes)
        {
            // CORRECCIÓN: Como en SQLite se guarda la etiqueta compuesta "PESADO - INICIO", 
            // la deshebramos para mandarla limpia a la API junto con el usuario global
            string etapaBase = mov.Etapa?.Split('-')[0].Trim() ?? "PESADO";
            string subEtapaBase = mov.Etapa?.Contains("FIN") == true ? "FIN" : "INICIO";

            var resultado = await LogEventAsync(etapaBase, mov.NumLote ?? "", subEtapaBase, App.UsuarioLogueadoId);

            if (resultado.IsSuccess)
            {
                mov.Sincronizado = true;
                await _db.UpdateAsync(mov);
                enviados++;
            }
            else
            {
                fallidos++;
            }
        }

        SentrySdk.CaptureMessage($"Sincronización completada: {enviados} enviados, {fallidos} fallidos");
        return (enviados, fallidos);
    }

    private Task GuardarLocalAsync(string numLote, string etapa, string observaciones, bool sincronizado)
        => _db.InsertAsync(new MovimientoLocal
        {
            NumLote = numLote,
            Etapa = etapa,
            Fecha = DateTime.Now,
            Sincronizado = sincronizado
        });

    private static string? ExtraerMensajeApi(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err)) return err.GetString();
            if (root.TryGetProperty("mensaje", out var msg)) return msg.GetString();
        }
        catch { }
        return null;
    }
}

// ============================================================
// RESULTADO TIPADO
// ============================================================
public class TraceResult
{
    public enum TipoResultado { Success, Offline, Fail }
    public TipoResultado Tipo { get; private set; }
    public string Message { get; private set; } = "";
    public string FechaOriginal { get; private set; } = "";
    public bool IsDuplicado { get; private set; } = false;

    public bool IsSuccess => Tipo == TipoResultado.Success;
    public bool IsOffline => Tipo == TipoResultado.Offline;
    public bool IsFail => Tipo == TipoResultado.Fail;

    public static TraceResult Ok(string msg, string fecha = "", bool esDuplicado = false)
        => new() { Tipo = TipoResultado.Success, Message = msg, FechaOriginal = fecha, IsDuplicado = esDuplicado };
    public static TraceResult Offline(string msg) => new() { Tipo = TipoResultado.Offline, Message = msg };
    public static TraceResult Fail(string msg) => new() { Tipo = TipoResultado.Fail, Message = msg };
}