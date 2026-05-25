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

    public EscaneoTiemposPage(string lote, string etapa)
    {
        InitializeComponent();
        _lote = lote;
        _etapa = etapa;

        // CORRECCIÓN: Usamos el constructor limpio inyectando la base de datos 
        // para que tome la URL dinámica de AppConfig y no rompa al cambiar de túnel ngrok
        _trace = new TraceabilityService(App.Database);

        LblInfo.Text = $"Lote: {_lote} | Etapa: {_etapa}";
        TxtScannerTiempos.Focus();
    }

    private async void OnScannerTiemposChanged(object sender, TextChangedEventArgs e)
    {
        string codigo = e.NewTextValue?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(codigo)) return;

        // Mapeo de subEtapa (Si es INICIO o FIN del proceso)
        string subEtapaApi = codigo switch
        {
            var c when c.Contains("INICIO") => "INICIO",
            var c when c.Contains("FIN") || c.Contains("FINAL") => "FIN",
            _ => ""
        };

        if (string.IsNullOrEmpty(subEtapaApi)) return;

        TxtScannerTiempos.Text = string.Empty;
        MostrarEstado("ENVIANDO...", "", Colors.Orange);

        // CORRECCIÓN DE LA FIRMA: Mandamos la etapa base del menú principal (_etapa), el lote, 
        // la subEtapa detectada por el switch (INICIO/FIN) y el usuario global logueado
        var result = await _trace.LogEventAsync(_etapa, _lote, subEtapaApi, App.UsuarioLogueadoId);

        if (result.IsSuccess)
        {
            if (result.IsDuplicado)
            {
                MostrarEstado("¡YA FUE ESCANEADO PREVIAMENTE!", "", Colors.Green);
            }
            else
            {
                MostrarEstado($"¡{subEtapaApi} REGISTRADO!", "", Colors.Green);
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
        else // IsFail
        {
            MostrarEstado("ERROR DE REGISTRO", "", Colors.Red);
            LblDetalleError.Text = result.Message;
            LblDetalleError.IsVisible = true;
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