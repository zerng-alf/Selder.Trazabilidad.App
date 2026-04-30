namespace Selder.Trazabilidad.App;

public partial class SeleccionEtapaPage : ContentPage
{

    public SeleccionEtapaPage() 
    {
        InitializeComponent();
        // Si tienes el LblLote en el XAML, puedes poner un mensaje genérico
        if (LblLote != null) LblLote.Text = "Seleccione la Etapa de Trabajo";
    }

    private async void OnEtapaSelected(object sender, EventArgs e)
    {
        var boton = (Button)sender;
        string etapa = boton.CommandParameter.ToString();

        // Ahora navegamos a MainPage pasando SOLO la etapa
        await Navigation.PushAsync(new MainPage(etapa));
    }
}