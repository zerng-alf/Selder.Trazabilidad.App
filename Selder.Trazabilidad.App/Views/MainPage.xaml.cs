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
    // VARIABLES DE CLASE (ESTADO DE LA PANTALLA)
    private string _etapaSeleccionada;             // Guarda la etapa actual (PESADO, GRANULADO, etc.)
    private readonly SQLiteAsyncConnection _db = App.Database; // Conexión activa a la base de datos local SQLite
    private readonly TraceabilityService _traceService;        // Servicio que conecta con la API en SQL Server
    bool estaProcesando = false;                   // Bandera (Flag) tipo semáforo para evitar que un doble escaneo rápido truene el flujo
    private CancellationTokenSource _scannerCts;   // Controla el temporizador de espera (delay) de la pistola Zebra

    // CONSTRUCTOR: Se ejecuta al abrir la pantalla
    public MainPage(string etapa)
    {
        InitializeComponent();
        _etapaSeleccionada = etapa; // Recibe la etapa seleccionada desde el menú anterior

        // Inicializa el servicio pasándole la base de datos local
        _traceService = new TraceabilityService(App.Database);

        // Llama a la creación de la tabla local en segundo plano (fire-and-forget)
        _ = InitializeDatabaseAsync();

        // CONFIGURACIÓN PARA ANDROID: Oculta el teclado táctil de la pantalla para que no estorbe al usar la Zebra
#if ANDROID
        var platformView = TxtScanner.Handler?.PlatformView as Android.Widget.EditText;
        if (platformView != null)
        {
            platformView.ShowSoftInputOnFocus = false; // Bloquea el teclado virtual nativo
        }
#endif

        // Configura los textos iniciales de la pantalla con la etapa correspondiente
        LblInstruccion.Text = $"ETAPA: {_etapaSeleccionada.ToUpper()} - ESCANEE LOTE";
        LblLoteCapturado.Text = "------";
    }

    // CREACIÓN DE TABLA LOCAL: Crea la tabla de movimientos en SQLite si aún no existe
    private async Task InitializeDatabaseAsync()
    {
        if (_db != null)
        {
            await _db.CreateTableAsync<MovimientoLocal>();
        }
    }

    // EVENTO AL MOSTRAR LA PANTALLA: Forza el foco automático al Entry invisible para recibir el escáner
    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Le damos 300ms a la interfaz para renderizarse antes de pedir el foco
        Task.Delay(300).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => {
            TxtScanner.Focus(); // Pone el cursor en el campo del escáner
            OcultarTecladoNativo();
        }));
    }

    // DETECTOR DE CARACTERES DEL Scanner: Se dispara cada que la pistola mete una letra/número
    private async void OnScannerTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue)) return; // Si limpiamos el campo por código, lo ignora

        string entradaActual = e.NewTextValue;

        // Reinicia el temporizador. Si el operador sigue metiendo caracteres, cancela la espera anterior
        _scannerCts?.Cancel();
        _scannerCts = new CancellationTokenSource();

        try
        {
            // Espera los milisegundos configurados en AppConfig para asegurar que la Zebra terminó de escribir
            await Task.Delay(AppConfig.ScannerDelayMs, _scannerCts.Token);

            // Limpia espacios y convierte a mayúsculas el código de barras completo
            string codigoCompleto = entradaActual.Trim().ToUpper();

            // Si el código es válido (3 o más caracteres), procesa el lote
            if (codigoCompleto.Length >= 3)
            {
                await ProcesarEntradaDirecta(codigoCompleto);
            }
        }
        catch (TaskCanceledException)
        {
            // El escáner sigue enviando letras, no hace nada y espera al siguiente caracter
        }
    }

    // TRUCO DE USABILIDAD: Fuerza el cierre definitivo de cualquier residuo del teclado en Android
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
                inputMethodManager.HideSoftInputFromWindow(platformView.WindowToken, 0); // Cierra la ventana del teclado
            }
        }
#endif
    }

    // NÚCLEO DEL SISTEMA: Procesa el lote escaneado, decide si es Inicio, Fin o una Tercera Lectura
    private async Task ProcesarEntradaDirecta(string codigoLote)
    {
        if (estaProcesando) return; // Si ya hay un escáner en cola, rebota los duplicados de hardware

        try
        {
            estaProcesando = true; // Prende el semáforo de bloqueo

            LoadingIndicator.IsRunning = true;
            BtnReset.IsEnabled = false;

            // Limpieza inmediata del campo para el siguiente lote
            MainThread.BeginInvokeOnMainThread(() => TxtScanner.Text = string.Empty);

            // 1. DETERMINACIÓN DEL ESTADO DE LA MATRIZ LOCAL EN SQLITE
            // Buscamos si existe algún registro de INICIO para este lote
            var movimientoInicio = await _db.Table<MovimientoLocal>()
                .Where(m => m.NumLote == codigoLote && m.Etapa == $"{_etapaSeleccionada} - INICIO")
                .OrderByDescending(m => m.Fecha)
                .FirstOrDefaultAsync();

            // Buscamos si ya existe también su registro de FIN correspondiente
            var movimientoFin = await _db.Table<MovimientoLocal>()
                .Where(m => m.NumLote == codigoLote && m.Etapa == $"{_etapaSeleccionada} - FIN")
                .OrderByDescending(m => m.Fecha)
                .FirstOrDefaultAsync();

            string subEtapaApi = "";
            string etiquetaLocal = "";
            bool esTerceraLectura = false;

            // EVALUACIÓN DE ESCENARIOS (Flujo de 3 pasos)
            if (movimientoInicio != null && movimientoFin != null)
            {
                // CASO 3: ¡TERCERA LECTURA! Ya se escaneó principio y fin anteriormente
                esTerceraLectura = true;
                subEtapaApi = "FIN"; // Mandamos FIN a la API por consistencia o seguridad
                etiquetaLocal = $"{_etapaSeleccionada} - FIN";
            }
            else if (movimientoInicio != null && movimientoFin == null)
            {
                // CASO 2: SEGUNDO ESCANEO (Ya tiene inicio, toca registrar el FIN)
                subEtapaApi = "FIN";
                etiquetaLocal = $"{_etapaSeleccionada} - FIN";
                MostrarEstado($"FINALIZANDO: {codigoLote}", Colors.Green);
            }
            else
            {
                // CASO 1: PRIMER ESCANEO (No tiene nada registrado, toca INICIO)
                subEtapaApi = "INICIO";
                etiquetaLocal = $"{_etapaSeleccionada} - INICIO";
                MostrarEstado($"INICIANDO: {codigoLote}", Colors.DarkCyan);
            }

            // 2. ENVÍO A SQL SERVER / API
            var result = await _traceService.LogEventAsync(
                _etapaSeleccionada,
                codigoLote,
                subEtapaApi,
                App.UsuarioLogueadoId,
                etiquetaLocal
            );

            // 3. ACTUALIZACIÓN DINÁMICA DE LA INTERFAZ
            if (result.IsSuccess || result.IsDuplicado || esTerceraLectura)
            {
                ContenedorInformacion.IsVisible = true; // Revela el bloque de datos inferior
                LblLoteCapturado.Text = codigoLote;

                // Si detectamos localmente que es la tercera lectura (o la API nos rebota el duplicado)
                if (esTerceraLectura || result.IsDuplicado)
                {
                    MostrarEstado("¡LOTE YA PROCESADO Y CERRADO!", Colors.Green);
                    LblInstruccion.Text = "PROCESO FINALIZADO COMPLETAMENTE";

                    // 1. OBTENER HORA DE INICIO: Prioriza lo que mandó la API de SQL Server, si no, usa el local
                    string fechaInicioStr = "---";
                    if (!string.IsNullOrEmpty(result.FechaInicio) && result.FechaInicio != "---")
                    {
                        fechaInicioStr = result.FechaInicio;
                    }
                    else if (movimientoInicio != null)
                    {
                        fechaInicioStr = movimientoInicio.Fecha.ToString("dd/MM/yyyy hh:mm:ss tt");
                    }
                    else if (!string.IsNullOrEmpty(result.FechaOriginal))
                    {
                        fechaInicioStr = result.FechaOriginal;
                    }

                    // 2. OBTENER HORA DE FIN: Prioriza el Fin del servidor, si no, usa el local
                    string fechaFinStr = "---";
                    if (!string.IsNullOrEmpty(result.FechaFin) && result.FechaFin != "---")
                    {
                        fechaFinStr = result.FechaFin;
                    }
                    else if (movimientoFin != null)
                    {
                        fechaFinStr = movimientoFin.Fecha.ToString("dd/MM/yyyy hh:mm:ss tt");
                    }
                    else if (!string.IsNullOrEmpty(result.FechaOriginal) && fechaInicioStr != result.FechaOriginal)
                    {
                        fechaFinStr = result.FechaOriginal;
                    }

                    // 3. ASIGNACIÓN FINAL A LAS ETIQUETAS DE LA INTERFAZ
                    LblHoraInicio.Text = $"Hora Inicio: {fechaInicioStr}";
                    LblHoraFin.Text = $"Hora Fin: {fechaFinStr}";
                }
                else
                {
                    // Flujo normal paso a paso (Primer o Segundo escaneo exitoso)
                    MostrarEstado($"¡{subEtapaApi} REGISTRADO!", Colors.Green);

                    if (subEtapaApi == "INICIO")
                    {
                        LblHoraInicio.Text = $"Hora Inicio: {result.FechaOriginal}";
                        LblHoraFin.Text = "Hora Fin: ---";
                        LblInstruccion.Text = "ESCANEE FIN DE PROCESO";
                    }
                    else if (subEtapaApi == "FIN")
                    {
                        if (movimientoInicio != null)
                        {
                            LblHoraInicio.Text = $"Hora Inicio: {movimientoInicio.Fecha:dd/MM/yyyy hh:mm:ss tt}";
                        }
                        LblHoraFin.Text = $"Hora Fin: {result.FechaOriginal}";
                        LblInstruccion.Text = "PROCESO FINALIZADO";
                    }
                }
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

    // ACTUALIZADOR VISUAL: Cambia los textos y el color del cuadro de estado principal
    private void MostrarEstado(string mensaje, Color color)
    {
        LblStatus.Text = mensaje;
        FrameStatus.BackgroundColor = color;
        // Si el fondo es amarillo (Offline), cambia el texto a negro para mantener el contraste y cumplir con la NOM
        LblStatus.TextColor = (color == Colors.Yellow) ? Colors.Black : Colors.White;
    }

    // BOTÓN DE LIMPIEZA: Resetea la interfaz a ceros para dejar la terminal lista para un lote nuevo
    private void OnResetClicked(object sender, EventArgs e)
    {
        LblLoteCapturado.Text = "------";
        LblHoraInicio.Text = "Hora Inicio: ---";
        LblHoraFin.Text = "Hora Fin: ---";

        // Oculta por completo el bloque inferior de datos
        ContenedorInformacion.IsVisible = false;

        // Regresa el cuadro de estado a Gris obscuro (Modo espera)
        MostrarEstado("ESPERANDO ESCANEO...", Color.FromArgb("#444"));
        LblInstruccion.Text = $"ETAPA: {_etapaSeleccionada.ToUpper()} - ESCANEE LOTE";
        TxtScanner.Focus(); // Regresa el cursor al escáner
    }

    // BOTÓN FÍSICO ATRÁS: Intercepta el botón de navegación nativo
    protected override bool OnBackButtonPressed()
    {
        RegresarPagina();
        return true; // Bloquea el comportamiento por defecto para controlar la salida
    }

    private async void RegresarPagina()
    {
        await Navigation.PopAsync(); // Saca la página del Stack y regresa al menú de etapas
    }

    // LOGOUT: Destruye la sesión del operador y lo expulsa al Login limpio
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Cerrar Sesión", "¿Está seguro que desea salir?", "Sí", "No");

        if (confirm)
        {
            SentrySdk.CaptureMessage("Usuario cerró sesión");

            // Jala el servicio de autenticación desde el contenedor global de dependencias
            var authService = App.Services.GetRequiredService<IAuthService>();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Limpia todo el historial de páginas y monta el LoginPage desde raíz para blindar la app
                Application.Current.MainPage = new NavigationPage(new LoginPage(authService));
            });
        }
    }
}