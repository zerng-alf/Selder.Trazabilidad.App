using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Selder.Trazabilidad.App.Configuration;

namespace Selder.Trazabilidad.App.Services;

public interface IAuthService
{
    Task<(bool Success, string Message, string NombreUsuario)> ValidateCredentialsAsync(string codigoUsuario, string codigoBarras);
}

public class AuthService : IAuthService
{
    public async Task<(bool Success, string Message, string NombreUsuario)> ValidateCredentialsAsync(string codigoUsuario, string codigoBarras)
    {
        if (string.IsNullOrEmpty(codigoUsuario) || string.IsNullOrEmpty(codigoBarras))
            return (false, "Usuario y contraseña son requeridos", "");

        try
        {
            
            string urlLogin = $"{AppConfig.ApiBaseUrl}/api/Trazabilidad/login";

            // Armamos el objeto con los nombres que espera la API
            var loginData = new
            {
                Usuario = codigoUsuario.Trim(),
                Password = codigoBarras.Trim()
            };

            string jsonPayload = JsonSerializer.Serialize(loginData);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var client = new HttpClient();
            // Usamos el timeout configurado en tu AppConfig
            client.Timeout = TimeSpan.FromSeconds(AppConfig.ApiTimeoutSeconds);

            var response = await client.PostAsync(urlLogin, content);
            string responseString = await response.Content.ReadAsStringAsync();

            var resultado = JsonSerializer.Deserialize<LoginApiResponse>(responseString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (response.IsSuccessStatusCode && resultado != null && resultado.Success)
            {
                return (true, "Login exitoso", resultado.Nombre);
            }

            return (false, resultado?.Message ?? "Usuario o contraseña incorrectos", "");
        }
        catch (Exception ex)
        {
            return (false, "Fallo de conexión con el servidor. Verifique su señal Wi-Fi.", "");
        }
    }
}

// DTO para recibir la respuesta de la API de forma segura
public class LoginApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Nombre { get; set; }
}