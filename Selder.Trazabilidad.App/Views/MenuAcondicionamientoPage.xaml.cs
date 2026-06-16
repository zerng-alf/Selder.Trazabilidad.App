using Microsoft.Maui.Controls; 
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
        // Manda a la pantalla del escáner pasándole "SURTIDO"
        await Navigation.PushAsync(new MainPage("SURTIDO"));
    }

    private async void OnAcondicionamientoProcesoClicked(object sender, EventArgs e)
    {
        // Manda a la pantalla del escáner pasándole "ACONDICIONAMIENTO"
        await Navigation.PushAsync(new MainPage("ACONDICIONAMIENTO"));
    }

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