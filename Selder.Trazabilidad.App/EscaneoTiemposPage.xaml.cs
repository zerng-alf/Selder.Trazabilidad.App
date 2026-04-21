using SQLite;

namespace Selder.Trazabilidad.App;

public partial class EscaneoTiemposPage : ContentPage
{
    string _lote;
    string _etapa;
    private readonly SQLiteAsyncConnection _db = App.Database;

    public EscaneoTiemposPage(string lote, string etapa)
    {
        InitializeComponent();
        _lote = lote;
        _etapa = etapa;
        LblInfo.Text = $"Lote: {_lote} | Etapa: {_etapa}";

        // TRUCO: Forzar el foco al entrar y cada vez que la página gane foco
        this.Appearing += (s, e) => TxtScannerTiempos.Focus();
    }

    // Si el usuario toca la pantalla por error, regresamos el foco al escáner
    protected override void OnAppearing()
    {
        base.OnAppearing();
        TxtScannerTiempos.Focus();
    }

    private async void OnScannerTiemposChanged(object sender, TextChangedEventArgs e)
    {
        // El escáner suele mandar un 'Enter' al final, por eso usamos Trim
        string codigo = e.NewTextValue?.Trim().ToUpper() ?? "";

        if (string.IsNullOrEmpty(codigo)) return;

        // Limpiamos el campo inmediatamente para el siguiente escaneo
        TxtScannerTiempos.Text = string.Empty;

        string accion = "";

        // Comparamos lo que trae el código de barras
        if (codigo.Contains("INICIO") || codigo == "START")
            accion = "INICIO";
        else if (codigo.Contains("FIN") || codigo == "END" || codigo == "FINAL")
            accion = "FIN";

        if (!string.IsNullOrEmpty(accion))
        {
            await RegistrarTiempo(accion);
        }
    }

    private async Task RegistrarTiempo(string accion)
    {
        DateTime ahora = DateTime.Now;
        string registroCompleto = $"{_etapa}_{accion}";

        try
        {
            // Guardar en SQLite local (Zebra)
            await _db.InsertAsync(new MovimientoLocal
            {
                NumLote = _lote,
                Etapa = registroCompleto,
                Fecha = ahora,
                Sincronizado = false
            });

            await DisplayAlert("Selder", $"¡{registroCompleto} registrado!\nHora: {ahora:HH:mm:ss}", "OK");

            // Si es el FIN de la etapa, regresamos a la pantalla de lotes
            if (accion == "FIN")
            {
                await Navigation.PopToRootAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            // Pase lo que pase, recuperamos el foco
            TxtScannerTiempos.Focus();
        }
    }
}