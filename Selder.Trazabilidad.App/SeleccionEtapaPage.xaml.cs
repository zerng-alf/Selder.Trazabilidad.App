namespace Selder.Trazabilidad.App;

public partial class SeleccionEtapaPage : ContentPage
{
    string _lote;

    public SeleccionEtapaPage(string lote)
    {
        InitializeComponent();
        _lote = lote;
        LblLote.Text = $"LOTE: {_lote}";
    }

    private async void OnEtapaSelected(object sender, EventArgs e)
    {
        var boton = (Button)sender;
        string etapa = boton.CommandParameter.ToString();

        // Esto abre la pantalla donde se escanea INICIO o FIN
        await Navigation.PushAsync(new EscaneoTiemposPage(_lote, etapa));
    }
}