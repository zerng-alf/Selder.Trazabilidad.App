using Microsoft.Maui.Controls;
using Sentry;
using Selder.Trazabilidad.App.Services;

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