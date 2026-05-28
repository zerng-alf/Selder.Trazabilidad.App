using Sentry;
using Selder.Trazabilidad.App.Services;

namespace Selder.Trazabilidad.App.Views;

public partial class SeleccionProcesoPage : ContentPage
{
    public SeleccionProcesoPage()
    {
        InitializeComponent();
    }

    // Al dar clic en Fabricación, lo mandas a tu pantalla actual de las 4 etapas
    private async void OnFabricacionClicked(object sender, EventArgs e)
    {
        // NOTA: Reemplaza "MenuEtapasPage" por el nombre real de tu vista de 4 etapas
        await Navigation.PushAsync(new SeleccionEtapaPage());
    }

    // Al dar clic en Acondicionamiento, lo mandas al nuevo sub-menú de 2 etapas
    private async void OnAcondicionamientoClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new MenuAcondicionamientoPage());
    }

    // Botón de escape seguro por si abrieron la app por error
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Cerrar Sesión", "¿Desea salir del menú principal?", "Sí", "No");
        if (confirm)
        {
            var authService = App.Services.GetRequiredService<IAuthService>();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current.MainPage = new NavigationPage(new LoginPage(authService));
            });
        }
    }
}