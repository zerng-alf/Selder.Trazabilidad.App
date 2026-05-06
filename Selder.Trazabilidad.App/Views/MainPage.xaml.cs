using Selder.Trazabilidad.App.Models;
using Selder.Trazabilidad.App.Helpers;
using Selder.Trazabilidad.App.Services;
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
    private string _bufferEscaneo = ""; // Aquí guardaremos el código letra por letra
    private CancellationTokenSource _scannerCts;

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
        // 1. Si el cambio fue porque nosotros limpiamos el campo (Text = ""), ignoramos.
        if (string.IsNullOrEmpty(e.NewTextValue)) return;

        // 2. Acumulamos lo que entró. 
        // NOTA: Usamos el último carácter porque la Zebra manda una ráfaga.
        string entradaActual = e.NewTextValue;

        // Reiniciamos el temporizador de espera
        _scannerCts?.Cancel();
        _scannerCts = new CancellationTokenSource();

        try
        {
            // Esperamos 800ms de "silencio" total del escáner
            await Task.Delay(800, _scannerCts.Token);

            // Si llegamos aquí, el escáner terminó de mandar datos.
            // Tomamos TODO lo que se acumuló en el Entry.
            string codigoCompleto = entradaActual.Trim().ToUpper();

            if (codigoCompleto.Length >= 3)
            {
                await ProcesarEntradaDirecta(codigoCompleto);
            }
        }
        catch (TaskCanceledException)
        {
            // El escáner sigue mandando letras, no hacemos nada todavía
        }
    }

    private async Task ProcesarEntradaDirecta(string codigoLote)
    {
        if (estaProcesando) return;

        try
        {
            estaProcesando = true;

            // LIMPIEZA INMEDIATA: Vaciamos el TxtScanner para que esté listo para el siguiente lote
            MainThread.BeginInvokeOnMainThread(() => TxtScanner.Text = string.Empty);

            // 1. LÓGICA DE DETECCIÓN (Usa la variable codigoLote que ya viene completa)
            var ultimoMovimiento = await _db.Table<MovimientoLocal>()
                .Where(m => m.NumLote == codigoLote && m.Etapa.StartsWith(_etapaSeleccionada))
                .OrderByDescending(m => m.Fecha)
                .FirstOrDefaultAsync();

            string subEtapaApi = "";
            string etiquetaLocal = "";

            if (ultimoMovimiento == null || ultimoMovimiento.Etapa.Contains("FIN"))
            {
                subEtapaApi = "INICIO";
                etiquetaLocal = $"{_etapaSeleccionada} - INICIO";
                MostrarEstado($"INICIANDO: {codigoLote}", Colors.DarkCyan);
            }
            else
            {
                subEtapaApi = "FIN";
                etiquetaLocal = $"{_etapaSeleccionada} - FIN";
                MostrarEstado($"FINALIZANDO: {codigoLote}", Colors.Green);
            }

            // 2. REGISTRO
            var result = await _traceService.LogEventAsync(subEtapaApi, codigoLote, etiquetaLocal);

            if (result.IsSuccess)
            {
                MostrarEstado($"¡{subEtapaApi} REGISTRADO!", Colors.Green);
                LblLoteCapturado.Text = codigoLote;
                LblInstruccion.Text = $"ÚLTIMO: {etiquetaLocal}";
            }
            else if (result.IsOffline)
            {
                MostrarEstado("GUARDADO LOCAL (SIN RED)", Colors.Yellow);
                await DisplayAlert("Modo Offline", "Sin conexión. Registro guardado en la Zebra.", "OK");
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
            // Pequeño respiro para la UI antes de recuperar el foco
            await Task.Delay(400);
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