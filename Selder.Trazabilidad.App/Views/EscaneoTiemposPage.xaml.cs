using SQLite;
using System.Text;
using System.Text.Json;
using Selder.Trazabilidad.App.Services;
using Selder.Trazabilidad.App.Helpers;

namespace Selder.Trazabilidad.App;

public partial class EscaneoTiemposPage : ContentPage
{
    private readonly string _lote;
    private readonly string _etapa;
    private readonly TraceabilityService _trace;

    public EscaneoTiemposPage(string lote, string etapa)
    {
        InitializeComponent();
        _lote = lote;
        _etapa = etapa;

        _trace = new TraceabilityService(
            App.Database,
            "https://nevaeh-biographical-overgratefully.ngrok-free.dev/api/Trazabilidad/registrar"
        );

        LblInfo.Text = $"Lote: {_lote} | Etapa: {_etapa}";
        TxtScannerTiempos.Focus();
    }

    private async void OnScannerTiemposChanged(object sender, TextChangedEventArgs e)
    {
        string codigo = e.NewTextValue?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(codigo)) return;

        // Mapeo igual que tu lógica original
        string etapaApi = codigo switch
        {
            var c when c.Contains("INICIO") => TraceabilityService.Etapa.Inicio,
            var c when c.Contains("FIN") || c.Contains("FINAL") => TraceabilityService.Etapa.Produccion,
            _ => ""
        };

        if (string.IsNullOrEmpty(etapaApi)) return;

        TxtScannerTiempos.Text = string.Empty;
        MostrarEstado("ENVIANDO...","", Colors.Orange);

        var result = await _trace.LogEventAsync(etapaApi, _lote);

        if (result.IsSuccess)
        {
            MostrarEstado($"¡{etapaApi} REGISTRADO!","", Colors.Green);
            if (etapaApi == TraceabilityService.Etapa.Produccion)
            {
                await Task.Delay(1500);
                await Navigation.PopAsync();
            }
        }
        else if (result.IsOffline)
        {
            MostrarEstado("GUARDADO LOCAL (SIN RED)","", Colors.Yellow);
            LblStatus.TextColor = Colors.Black;
            LblDetalleError.Text = result.Message;
            LblDetalleError.IsVisible = true;
        }
        else // IsFail — error de datos (lote no encontrado, etapa inválida)
        {
            MostrarEstado("ERROR DE REGISTRO","", Colors.Red);
            LblDetalleError.Text = result.Message;
            LblDetalleError.IsVisible = true;
        }
    }

    private void MostrarEstado(string mensaje, string detalle, Color colorFondo)
    {
        LblStatus.Text = mensaje;
        LblDetalleError.Text = detalle;
        FrameStatus.BackgroundColor = colorFondo;

        // Si el fondo es Amarillo (Local), ponemos texto negro. Si no, blanco.
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