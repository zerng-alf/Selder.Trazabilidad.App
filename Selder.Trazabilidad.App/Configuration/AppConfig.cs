namespace Selder.Trazabilidad.App.Configuration;

public static class AppConfig
{
    // ============================================================
    // CONFIGURACIÓN CENTRALIZADA - CAMBIAR AQUÍ LOS VALORES
    // ============================================================

    // URL base de la API - Se centraliza para evitar duplicación
    // IMPORTANTE: Cambiar por la URL de producción cuando esté lista
    public static string ApiBaseUrl => "https://nevaeh-biographical-overgratefully.ngrok-free.dev";

    // Endpoint de trazabilidad
    public static string TrazabilidadEndpoint => $"{ApiBaseUrl}/api/Trazabilidad/registrar";

    // ============================================================
    // CONEXIÓN A BASE DE DATOS CORPORATIVA (SQL Server)
    // ============================================================

    // Cambiar por la cadena de conexión real de producción
    public static string ConnectionString => "Data Source=130.107.20.200,1433;Initial Catalog=PRUEBASLEYVA;User ID=BECARIO;Password=123456789;TrustServerCertificate=True";
    //public static string ConnectionString => "Data Source=seldersqlmex\\seldersql;Initial Catalog=PRUEBASLEYVA; Integrated Security=True;TrustServerCertificate=True";

    // ============================================================
    // CONFIGURACIÓN DE SEGURIDAD
    // ============================================================

    // Habilitar validación de certificados SSL (RECOMENDADO: true en producción)
    public static bool ValidateSslCertificates => true; // Temporalmente false para ngrok

    // Timeout para llamadas a la API (en segundos)
    public static int ApiTimeoutSeconds => 8;

    // ============================================================
    // CONFIGURACIÓN DE SINCRONIZACIÓN
    // ============================================================

    // Delay inicial al iniciar la app antes de sincronizar (ms)
    public static int SyncInitialDelayMs => 2000;

    // Delay de espera para escáner (ms)
    public static int ScannerDelayMs => 800;
}