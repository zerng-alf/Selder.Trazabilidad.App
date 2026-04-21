using Microsoft.Maui.Controls;

namespace Selder.Trazabilidad.App
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            if (TxtUsuario.Text == "admin" && TxtPassword.Text == "1234")
            {
                // Llamamos a MainPage sin parámetros para que no marque error
                await Navigation.PushAsync(new MainPage());
            }
            else
            {
                LblError.Text = "Usuario o contraseña incorrectos";
            }
        }
    }
}