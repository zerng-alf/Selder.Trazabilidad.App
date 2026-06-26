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

            // Validación estricta: Ambos campos deben tener datos para que la API de Selder los procese
            if (string.IsNullOrWhiteSpace(TxtUsuario.Text) || string.IsNullOrWhiteSpace(TxtPassword.Text))
            {
                LblError.Text = "Por favor ingrese usuario y contraseña";
                return;
            }

            BtnLogin.IsEnabled = false;
            BtnLogin.Text = "Verificando...";

            try
            {
                // Enviamos los datos capturados de ambos escaneos
                var (success, message, nombreUsuario) = await _authService.ValidateCredentialsAsync(TxtUsuario.Text.Trim(), TxtPassword.Text.Trim());

                if (success)
                {
                    _intentosFallidos = 0;
                    SentrySdk.CaptureMessage($"Login exitoso: {nombreUsuario}");

                    // PERSISTENCIA: Guardamos el número de empleado de manera estática y global
                    App.UsuarioLogueadoId = TxtUsuario.Text.Trim();

                    // Cambiamos la MainPage de raíz para limpiar el historial y blindar el botón "Atrás"
                    // Dentro del éxito del Login en LoginPage.xaml.cs:
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Cambiamos la raíz para que entre al nuevo menú de dos secciones
                        Application.Current.MainPage = new NavigationPage(new Views.SeleccionProcesoPage());
                    });
                }
                else
                {
                    _intentosFallidos++;
                    SentrySdk.CaptureMessage($"Intento de login fallido. Intento: {_intentosFallidos}/{MaxIntentos}");

                    // Si falla el acceso, limpiamos la contraseña para que vuelvan a escanear la credencial
                    TxtPassword.Text = string.Empty;
                    TxtPassword.Focus();

                    if (_intentosFallidos >= MaxIntentos)
                    {
                        BtnLogin.IsEnabled = false;
                        BtnLogin.Text = "BLOQUEADO";
                        BtnLogin.BackgroundColor = Colors.Gray;
                        LblError.Text = "Acceso bloqueado. Demasiados intentos fallidos.";
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
                TxtPassword.Text = string.Empty;
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

        // ============================================================
        // CONTROLADOR DE ENTER Y ESCÁNER
        // ============================================================
        private void OnEntryCompleted(object sender, EventArgs e)
        {
            // PASO 1: Si dan enter o escanean en el Usuario...
            if (sender == TxtUsuario)
            {
                if (!string.IsNullOrWhiteSpace(TxtUsuario.Text))
                {
                    // Mandamos el foco en automático a la contraseña para recibir el segundo escaneo
                    TxtPassword.Focus();
                }
            }
            // PASO 2: ¡EL GATILLAZO FINAL! Si el escaner mete la contraseña y manda el Enter automático...
            else if (sender == TxtPassword)
            {
                if (!string.IsNullOrWhiteSpace(TxtPassword.Text) && !string.IsNullOrWhiteSpace(TxtUsuario.Text))
                {
                    // ¡LUEGO LUEGO ENTRA! Dispara el método de Login directamente
                    OnLoginClicked(BtnLogin, EventArgs.Empty);
                }
            }
        }

        private async void OnTxtPasswordTextChanged(object sender, TextChangedEventArgs e)
        {
            // Si la Honeywell ya inyectó caracteres en la contraseña y el usuario está lleno...
            if (!string.IsNullOrWhiteSpace(TxtPassword.Text) && !string.IsNullOrWhiteSpace(TxtUsuario.Text))
            {
                // Como la Honeywell escribe a ráfaga ultra rápida, capturamos el texto y esperamos 300ms
                string textoActual = TxtPassword.Text;
                await Task.Delay(300);

                // Si el texto ya no cambió después de la espera, significa que la pistola terminó de escribir
                if (TxtPassword.Text == textoActual)
                {
                    // ENTRA Ejecuta el login de forma automática sin pedir Enter físico
                    OnLoginClicked(BtnLogin, EventArgs.Empty);
                }
            }
        }
    }
}