using Selder.Trazabilidad.App.Models;
using Selder.Trazabilidad.App.Helpers;
using Selder.Trazabilidad.App.Services; // Importante para el Servicio
using SQLite;
using System.Text;
using System.Text.Json;

namespace Selder.Trazabilidad.App;

public partial class MainPage : ContentPage
{
    private string _etapaSeleccionada;
    private readonly SQLiteAsyncConnection _db = App.Database;
    private readonly TraceabilityService _traceService; // Inyectamos el servicio
    bool estaProcesando = false;

    public MainPage(string etapa)
    {
        InitializeComponent();
        _etapaSeleccionada = etapa;

        // Inicializamos el servicio con la URL que ya tenías configurada
        _traceService = new TraceabilityService(
            App.Database,
            "https://nevaeh-biographical-overgratefully.ngrok-free.dev/api/Trazabilidad/registrar"
        );

        if (_db != null)
        {
            _db.CreateTableAsync<MovimientoLocal>().Wait();
        }

#if ANDROID
        var platformView = TxtScanner.Handler?.PlatformView as Android.Widget.EditText;
        if (platformView != null)
        {
            platformView.ShowSoftInputOnFocus = false;
        }
#endif

        LblInstruccion.Text = $"ETAPA: {_etapaSeleccionada} - ESCANEE LOTE";
        LblLoteCapturado.Text = "---";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Task.Delay(300).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => {
            TxtScanner.Focus();
        }));
    }

    private async void OnScannerTextChanged(object sender, TextChangedEventArgs e)
    {
        string codigoCompleto = await ScannerHelper.ValidarCodigoCompleto(e.NewTextValue);
        if (codigoCompleto == null) return;

        await ProcesarEntradaDirecta(codigoCompleto);
    }

    private async Task ProcesarEntradaDirecta(string codigoLote)
    {
        if (estaProcesando) return;

        if (string.IsNullOrEmpty(codigoLote) || codigoLote.Length < 3) return;

        try
        {
            estaProcesando = true;

            MainThread.BeginInvokeOnMainThread(() => TxtScanner.Text = string.Empty);

            // 1. LÓGICA DE DETECCIÓN (Busca en SQLite si ya se inició esta etapa)
            var ultimoMovimiento = await _db.Table<MovimientoLocal>()
                .Where(m => m.NumLote == codigoLote && m.Etapa.StartsWith(_etapaSeleccionada))
                .OrderByDescending(m => m.Fecha)
                .FirstOrDefaultAsync();

            string subEtapaApi = ""; // Lo que entiende el SQL Server (Tu API)
            string etiquetaLocal = ""; // Lo que guardamos para el historial interno

            // Comprobamos si el último registro fue un cierre o no existe
            if (ultimoMovimiento == null || ultimoMovimiento.Etapa.Contains("FIN"))
            {
                subEtapaApi = "INICIO"; // Coincide con el switch de tu API
                etiquetaLocal = $"{_etapaSeleccionada} - INICIO";
                MostrarEstado($"INICIANDO: {codigoLote}", Colors.DarkCyan);
            }
            else
            {
                // CAMBIO CLAVE: Usamos "FIN" para que tu API actualice FECHAFIN
                subEtapaApi = "FIN";
                etiquetaLocal = $"{_etapaSeleccionada} - FIN";
                MostrarEstado($"FINALIZANDO: {codigoLote}", Colors.Green);
            }

            // 2. REGISTRO: Mandamos "INICIO" o "FIN" a la API y la etiqueta completa al SQLite
            var result = await _traceService.LogEventAsync(subEtapaApi, codigoLote, etiquetaLocal);

            if (result.IsSuccess)
            {
                // Usamos subEtapaApi para el mensaje de éxito (INICIO o FIN)
                MostrarEstado($"¡{subEtapaApi} REGISTRADO!", Colors.Green);
                LblLoteCapturado.Text = codigoLote;
                LblInstruccion.Text = $"ÚLTIMO: {etiquetaLocal}";
            }
            else if (result.IsOffline)
            {
                MostrarEstado("GUARDADO LOCAL (SIN RED)", Colors.Yellow);
                await DisplayAlert("Modo Offline", "Sin conexión. Se sincronizará después.", "OK");
                LblLoteCapturado.Text = codigoLote;
            }
            else
            {
                MostrarEstado("ERROR DE REGISTRO", Colors.Red);
                await DisplayAlert("Fallo", result.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error Crítico", ex.Message, "OK");
        }
        finally
        {
            estaProcesando = false;
            await Task.Delay(100);
            TxtScanner.Focus();
        }
    }

    private void MostrarEstado(string mensaje, Color color)
    {
        LblStatus.Text = mensaje;
        FrameStatus.BackgroundColor = color;

        // Ajuste de contraste para el color amarillo
        LblStatus.TextColor = (color == Colors.Yellow) ? Colors.Black : Colors.White;
    }

    private void OnResetClicked(object sender, EventArgs e)
    {
        LblLoteCapturado.Text = "---";
        MostrarEstado("ESPERANDO ESCANEO...", Color.FromArgb("#444"));
        LblInstruccion.Text = $"ETAPA: {_etapaSeleccionada} - ESCANEE LOTE";
        TxtScanner.Focus();
    }

    protected override bool OnBackButtonPressed()
    {
        RegresarPagina();
        return true;
    }

    private async void RegresarPagina()
    {
        await Navigation.PopAsync();
    }
}