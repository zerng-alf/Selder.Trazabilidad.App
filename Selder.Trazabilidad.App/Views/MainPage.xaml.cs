using Selder.Trazabilidad.App.Models;
using Selder.Trazabilidad.App.Helpers;
using Selder.Trazabilidad.App.Services;
using Selder.Trazabilidad.App.Configuration;
using SQLite;
using System.Text;
using System.Text.Json;
using Sentry;

namespace Selder.Trazabilidad.App;

public partial class MainPage : ContentPage
{
    private string _etapaSeleccionada;
    private readonly SQLiteAsyncConnection _db = App.Database;
    private readonly TraceabilityService _traceService;
    bool estaProcesando = false;
    private CancellationTokenSource _scannerCts;

    public MainPage(string etapa)
    {
        InitializeComponent();
        _etapaSeleccionada = etapa;

        _traceService = new TraceabilityService(App.Database);

        _ = InitializeDatabaseAsync();

        // Configuración específica para Android para mitigar el teclado
#if ANDROID
        var platformView = TxtScanner.Handler?.PlatformView as Android.Widget.EditText;
        if (platformView != null)
        {
            platformView.ShowSoftInputOnFocus = false;
        }
#endif

        LblInstruccion.Text = $"ETAPA: {_etapaSeleccionada.ToUpper()} - ESCANEE LOTE";
        LblLoteCapturado.Text = "------";
    }

    private async Task InitializeDatabaseAsync()
    {
        if (_db != null)
        {
            await _db.CreateTableAsync<MovimientoLocal>();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Task.Delay(300).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => {
            TxtScanner.Focus();
            OcultarTecladoNativo();
        }));
    }

    private async void OnScannerTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue)) return;

        string entradaActual = e.NewTextValue;

        _scannerCts?.Cancel();
        _scannerCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(AppConfig.ScannerDelayMs, _scannerCts.Token);

            string codigoCompleto = entradaActual.Trim().ToUpper();

            if (codigoCompleto.Length >= 3)
            {
                await ProcesarEntradaDirecta(codigoCompleto);
            }
        }
        catch (TaskCanceledException)
        {
            // El escáner sigue enviando caracteres
        }
    }

    private void OcultarTecladoNativo()
    {
#if ANDROID
        var platformView = TxtScanner.Handler?.PlatformView as Android.Widget.EditText;
        if (platformView != null)
        {
            platformView.ShowSoftInputOnFocus = false;
            
            var contexto = Android.App.Application.Context;
            var inputMethodManager = contexto.GetSystemService(Android.Content.Context.InputMethodService) as Android.Views.InputMethods.InputMethodManager;
            
            if (inputMethodManager != null && platformView.WindowToken != null)
            {
                inputMethodManager.HideSoftInputFromWindow(platformView.WindowToken, 0);
            }
        }
#endif
    }

    private async Task ProcesarEntradaDirecta(string codigoLote)
    {
        if (estaProcesando) return;

        try
        {
            estaProcesando = true;

            LoadingIndicator.IsRunning = true;
            BtnReset.IsEnabled = false;

            MainThread.BeginInvokeOnMainThread(() => TxtScanner.Text = string.Empty);

            // LÓGICA DE DETECCIÓN LOCAL
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

            // CONSUMO DEL SERVICIO: Pasamos la etapa matricial, lote, subetapa e ID del operador logueado
            var result = await _traceService.LogEventAsync(
                _etapaSeleccionada,
                codigoLote,
                subEtapaApi,
                App.UsuarioLogueadoId,
                etiquetaLocal
            );

            if (result.IsSuccess)
            {
                // REQUERIMIENTO DEL DIBUJO (Paso 4): Revelar las horas y guías al escanear
                ContenedorInformacion.IsVisible = true;

                if (result.IsDuplicado)
                {
                    // REQUERIMIENTO: Si ya existe en la BD, pintar verde y alertar duplicado
                    MostrarEstado("¡YA FUE ESCANEADO PREVIAMENTE!", Colors.Green);
                }
                else
                {
                    MostrarEstado($"¡{subEtapaApi} REGISTRADO!", Colors.Green);
                }

                // REQUERIMIENTO: Asignar tiempos y actualizar instrucciones de forma dinámica
                if (subEtapaApi == "INICIO")
                {
                    LblHoraInicio.Text = $"Hora Inicio: {result.FechaOriginal}";
                    LblInstruccion.Text = "ESCANEE FIN DE PROCESO";
                }
                else if (subEtapaApi == "FIN")
                {
                    LblHoraFin.Text = $"Hora Fin: {result.FechaOriginal}";
                    LblInstruccion.Text = "PROCESO FINALIZADO";
                }

                LblLoteCapturado.Text = codigoLote;
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
            SentrySdk.CaptureException(ex);
            await DisplayAlert("Error Crítico", "Ocurrió un problema. Intente de nuevo.", "OK");
        }
        finally
        {
            estaProcesando = false;
            LoadingIndicator.IsRunning = false;
            BtnReset.IsEnabled = true;

            await Task.Delay(400);
            TxtScanner.Focus();
            OcultarTecladoNativo();
        }
    }

    private void MostrarEstado(string mensaje, Color color)
    {
        LblStatus.Text = mensaje;
        FrameStatus.BackgroundColor = color;
        LblStatus.TextColor = (color == Colors.Yellow) ? Colors.Black : Colors.White;
    }

    private void OnResetClicked(object sender, EventArgs e)
    {
        LblLoteCapturado.Text = "------";
        LblHoraInicio.Text = "Hora Inicio: ---";
        LblHoraFin.Text = "Hora Fin: ---";

        // REQUERIMIENTO DEL DIBUJO (Paso 3): Regresar a vista ultra simple ocultando la información inferior
        ContenedorInformacion.IsVisible = false;

        MostrarEstado("ESPERANDO ESCANEO...", Color.FromArgb("#444"));
        LblInstruccion.Text = $"ETAPA: {_etapaSeleccionada.ToUpper()} - ESCANEE LOTE";
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

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Cerrar Sesión", "¿Está seguro que desea salir?", "Sí", "No");

        if (confirm)
        {
            SentrySdk.CaptureMessage("Usuario cerró sesión");
            var authService = App.Services.GetRequiredService<IAuthService>();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current.MainPage = new NavigationPage(new LoginPage(authService));
            });
        }
    }
}