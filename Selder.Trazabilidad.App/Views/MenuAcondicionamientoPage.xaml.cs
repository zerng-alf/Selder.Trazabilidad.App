using Microsoft.Maui.Controls; // o Xamarin.Forms si aplica
using Sentry;
using Selder.Trazabilidad.App.Services;

namespace Selder.Trazabilidad.App.Views;

public partial class MenuAcondicionamientoPage : ContentPage
{
    public MenuAcondicionamientoPage()
    {

        InitializeComponent(); 
    }

    private async void OnSurtidoClicked(object sender, EventArgs e)
    {
        // Manda a tu pantalla del escáner pasándole "SURTIDO"
        await Navigation.PushAsync(new MainPage("SURTIDO"));
    }

    private async void OnAcondicionamientoProcesoClicked(object sender, EventArgs e)
    {
        // Manda a tu pantalla del escáner pasándole "ACONDICIONAMIENTO"
        await Navigation.PushAsync(new MainPage("ACONDICIONAMIENTO"));
    }
}