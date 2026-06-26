using Microsoft.Data.SqlClient;
using Selder.Trazabilidad.App.Helpers;
using Selder.Trazabilidad.App.Services;
using SQLite;
using System.Text;
using System.Text.Json;

namespace Selder.Trazabilidad.App;

public partial class EscaneoTiemposPage : ContentPage
{
    private readonly string _lote;
    private readonly string _etapa;
    private readonly TraceabilityService _trace;
    private CancellationTokenSource _scannerCts;
    private bool _estaProcesando;

    public EscaneoTiemposPage(string lote, string etapa)
    {
        InitializeComponent();
        _lote = lote;
        _etapa = etapa;

        _trace = new TraceabilityService(App.Database);

        LblInfo.Text = $"Lote: {_lote} | Etapa: {_etapa}";
        TxtScannerTiempos.Focus();
    }

    private async void OnScannerTiemposChanged(object sender, TextChangedEventArgs e)
    {
        if (_estaProcesando) return;

        string codigo = e.NewTextValue?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(codigo)) return;

        _scannerCts?.Cancel();
        _scannerCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(400, _scannerCts.Token);

            string codigoFinal = codigo;

            string subEtapaApi = codigoFinal switch
            {
                var c when c.Contains("INICIO") => "INICIO",
                var c when c.Contains("FIN") || c.Contains("FINAL") => "FIN",
                _ => ""
            };

            if (string.IsNullOrEmpty(subEtapaApi)) return;

            _estaProcesando = true;
            TxtScannerTiempos.Text = string.Empty;
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            MostrarEstado("ENVIANDO...", "", Colors.Orange);

            var result = await _trace.LogEventAsync(_etapa, _lote, subEtapaApi, App.UsuarioLogueadoId);

            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;

            if (result.IsSuccess)
            {
                if (result.IsDuplicado)
                {
                    MostrarEstado("YA FUE ESCANEADO PREVIAMENTE", "", Colors.Green);
                }
                else
                {
                    MostrarEstado($"{subEtapaApi} REGISTRADO", "", Colors.Green);
                }

                if (subEtapaApi == "FIN")
                {
                    await Task.Delay(1500);
                    await Navigation.PopAsync();
                }
            }
            else if (result.IsOffline)
            {
                MostrarEstado("GUARDADO LOCAL (SIN RED)", "", Colors.Yellow);
                LblStatus.TextColor = Colors.Black;
                LblDetalleError.Text = result.Message;
                LblDetalleError.IsVisible = true;
            }
            else
            {
                MostrarEstado("ERROR DE REGISTRO", "", Colors.Red);
                LblDetalleError.Text = result.Message;
                LblDetalleError.IsVisible = true;
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _estaProcesando = false;
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void MostrarEstado(string mensaje, string detalle, Color colorFondo)
    {
        LblStatus.Text = mensaje;
        LblDetalleError.Text = detalle;
        FrameStatus.BackgroundColor = colorFondo;

        if (colorFondo == Colors.Yellow)
        {
            LblStatus.TextColor = Colors.Black;
            LblDetalleError.TextColor = Colors.Black;
        }
        else
        {
            LblStatus.TextColor = Colors.White;
            LblDetalleError.TextColor = Colors.White;
        }

        LblDetalleError.IsVisible = !string.IsNullOrEmpty(detalle);
    }
}