using System;
using Microsoft.Maui.Controls;
using Selder.Trazabilidad.App.Services;
using Sentry;

namespace Selder.Trazabilidad.App
{
    public partial class LoginPage : ContentPage
    {
        private readonly IAuthService _authService;

        // Contador de seguridad para bloqueo temporal
        private int _intentosFallidos = 0;
        private const int MaxIntentos = 3;

        public LoginPage(IAuthService authService)
        {
            InitializeComponent();
            _authService = authService;
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            if (_intentosFallidos >= MaxIntentos)
            {
                LblError.Text = "Aplicación bloqueada por demasiados intentos fallidos.";
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtUsuario.Text) || string.IsNullOrWhiteSpace(TxtPassword.Text))
            {
                LblError.Text = "Por favor ingrese usuario y contraseña";
                return;
            }

            BtnLogin.IsEnabled = false;
            BtnLogin.Text = "Verificando...";

            try
            {
                var (success, message, nombreUsuario) = await _authService.ValidateCredentialsAsync(TxtUsuario.Text, TxtPassword.Text);

                if (success)
                {
                    _intentosFallidos = 0;
                    SentrySdk.CaptureMessage($"Login exitoso: {nombreUsuario}");

                    // PERSISTENCIA: Guardamos el número de empleado de manera estática y global
                    App.UsuarioLogueadoId = TxtUsuario.Text.Trim();

                    // Cambiamos la MainPage de raíz para limpiar el historial y blindar el botón "Atrás"
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Application.Current.MainPage = new NavigationPage(new SeleccionEtapaPage());
                    });
                }
                else
                {
                    _intentosFallidos++;
                    SentrySdk.CaptureMessage($"Intento de login fallido. Intento: {_intentosFallidos}/{MaxIntentos}");

                    if (_intentosFallidos >= MaxIntentos)
                    {
                        BtnLogin.IsEnabled = false;
                        BtnLogin.Text = "BLOQUEADO";
                        BtnLogin.BackgroundColor = Colors.Gray;
                        LblError.Text = "Acceso bloqueada. Demasiados intentos fallidos.";
                    }
                    else
                    {
                        int restantes = MaxIntentos - _intentosFallidos;
                        LblError.Text = $"{message} (Te quedan {restantes} intento{(restantes > 1 ? "s" : "")})";
                    }
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                LblError.Text = "Error al iniciar sesión";
            }
            finally
            {
                if (_intentosFallidos < MaxIntentos)
                {
                    BtnLogin.IsEnabled = true;
                    BtnLogin.Text = "INICIAR SESIÓN";
                }
            }
        }

        private void OnEntryCompleted(object sender, EventArgs e)
        {
            if (sender == TxtUsuario && !string.IsNullOrWhiteSpace(TxtUsuario.Text))
            {
                TxtPassword.Focus();
            }
            else if (sender == TxtPassword)
            {
                OnLoginClicked(this, EventArgs.Empty);
            }
        }

        protected override bool OnBackButtonPressed()
        {
            // Bloquea el botón físico de Android en el login
            return true;
        }
    }
}