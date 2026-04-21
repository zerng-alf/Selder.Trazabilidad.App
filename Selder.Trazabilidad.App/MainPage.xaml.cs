using SQLite;
using System.Text;
using System.Text.Json;

namespace Selder.Trazabilidad.App;

public partial class MainPage : ContentPage
{
    string loteActual = "";
    private readonly SQLiteAsyncConnection _db = App.Database;

    bool estaProcesando = false;
    DateTime ultimaTecla;
    bool timerCorriendo = false;

    private readonly string urlApi = "https://130.107.20.128:7290/api/Trazabilidad/registrar";

    private static readonly HttpClient _clienteSincronizado = new HttpClient(new HttpClientHandler()
    {
        ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
    })
    { Timeout = TimeSpan.FromSeconds(8) };

    public MainPage()
    {
        InitializeComponent();

        if (_db != null)
        {
            _db.CreateTableAsync<MovimientoLocal>().Wait();
        }

        TxtScanner.Focus();
    }

    private void OnScannerTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.NewTextValue)) return;
        ultimaTecla = DateTime.Now;

        if (!timerCorriendo)
        {
            timerCorriendo = true;
            this.Dispatcher.StartTimer(TimeSpan.FromMilliseconds(400), () =>
            {
                if ((DateTime.Now - ultimaTecla).TotalMilliseconds >= 400)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await ProcesarEntradaDirecta();
                        timerCorriendo = false;
                    });
                    return false;
                }
                return true;
            });
        }
    }

    private async Task ProcesarEntradaDirecta()
    {
        if (estaProcesando) return;

        string codigo = TxtScanner.Text?.Trim() ?? "";
        TxtScanner.Text = string.Empty;

        if (string.IsNullOrEmpty(codigo)) return;

        try
        {
            estaProcesando = true;

            if (LblLoteCapturado.Text == "---")
            {
                string soloNumeros = new string(codigo.Where(char.IsDigit).ToArray());

                if (!string.IsNullOrEmpty(soloNumeros))
                {
                    loteActual = soloNumeros;

                    await Navigation.PushAsync(new SeleccionEtapaPage(soloNumeros));

                    LblLoteCapturado.Text = loteActual;
                    MostrarEstado($"LOTE: {loteActual}", Colors.DarkCyan);
                    LblInstruccion.Text = "ESCANEÉ LA ETAPA (INICIO/FINALIZADO)";
                }
            }
            else
            {
                bool exito = await RegistrarMovimiento(loteActual, codigo.ToUpper());

                if (exito)
                {
                    loteActual = "";
                    LblLoteCapturado.Text = "---";
                    LblInstruccion.Text = "ESCANEÉ EL LOTE";
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            estaProcesando = false;
            TxtScanner.Focus();
        }
    }

    private async Task<bool> RegistrarMovimiento(string lote, string etapa)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            MostrarEstado("ENVIANDO...", Colors.Orange);
        });

        var datos = new { numLote = lote, etapa = etapa };
        var json = JsonSerializer.Serialize(datos);
        var contenido = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var respuesta = await _clienteSincronizado.PostAsync(urlApi, contenido).ConfigureAwait(false);

            if (respuesta.IsSuccessStatusCode)
            {
                await _db.InsertAsync(new MovimientoLocal { NumLote = lote, Etapa = etapa, Fecha = DateTime.Now, Sincronizado = true });

                MainThread.BeginInvokeOnMainThread(() => {
                    MostrarEstado("¡EXITO! REGISTRADO", Colors.Green);
                });
                return true;
            }
            else
            {
                string errorServer = await respuesta.Content.ReadAsStringAsync();
                throw new Exception($"Servidor respondió: {respuesta.StatusCode}\n{errorServer}");
            }
        }
        catch (Exception ex)
        {
            await _db.InsertAsync(new MovimientoLocal { NumLote = lote, Etapa = etapa, Fecha = DateTime.Now, Sincronizado = false });

            MainThread.BeginInvokeOnMainThread(async () => {
                MostrarEstado("GUARDADO LOCAL (SIN RED)", Colors.Yellow);
                await DisplayAlert("Fallo de Envío", $"Guardado en Zebra local.\nMotivo: {ex.Message}", "OK");
            });

            return true;
        }
    }

    private void MostrarEstado(string mensaje, Color color)
    {
        LblStatus.Text = mensaje;
        FrameStatus.BackgroundColor = color;
    }

    private void OnResetClicked(object sender, EventArgs e)
    {
        loteActual = "";
        LblLoteCapturado.Text = "---";
        MostrarEstado("ESPERANDO ESCANEO...", Color.FromArgb("#444"));
        LblInstruccion.Text = "ESCANEÉ EL LOTE";
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