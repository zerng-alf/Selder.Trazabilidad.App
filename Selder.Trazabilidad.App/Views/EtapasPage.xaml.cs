using Selder.Trazabilidad.App.Services;

namespace Selder.Trazabilidad.App;

[Obsolete("Esta página está deprecated. Usar MainPage o EscaneoTiemposPage en su lugar.")]
public partial class EtapasPage : ContentPage
{
    string loteCapturado;
    private CancellationTokenSource _scannerCts;
    private bool _estaProcesando;
    private readonly TraceabilityService _trace;

    public EtapasPage(string lote)
    {
        InitializeComponent();
        loteCapturado = lote;
        LblLoteInfo.Text = $"LOTE: {lote}";

        _trace = new TraceabilityService(App.Database);
        TxtScannerEtapa.Focus();
    }

    private async void OnScannerEtapaChanged(object sender, TextChangedEventArgs e)
    {
        if (_estaProcesando) return;

        string codigo = e.NewTextValue?.Trim().ToUpper();
        if (string.IsNullOrEmpty(codigo)) return;

        _scannerCts?.Cancel();
        _scannerCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(400, _scannerCts.Token);

            string subEtapa = codigo switch
            {
                "INICIO" => "INICIO",
                "FIN" => "FIN",
                _ => null
            };

            if (subEtapa == null) return;

            _estaProcesando = true;
            TxtScannerEtapa.Text = string.Empty;

            var result = await _trace.LogEventAsync("GRANULADO", loteCapturado, subEtapa, App.UsuarioLogueadoId);

            if (result.IsSuccess)
            {
                await DisplayAlert("Éxito", $"{subEtapa} de etapa registrado", "OK");
                if (subEtapa == "FIN")
                    await Navigation.PopAsync();
            }
            else if (result.IsOffline)
            {
                await DisplayAlert("Modo Offline", "Guardado localmente. Se sincronizará después.", "OK");
            }
            else
            {
                await DisplayAlert("Error", result.Message, "OK");
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _estaProcesando = false;
            TxtScannerEtapa.Focus();
        }
    }
}