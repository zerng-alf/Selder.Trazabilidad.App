using Microsoft.Extensions.Logging;
using SQLite;
using Sentry;

namespace Selder.Trazabilidad.App
{

    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSentry(options =>
                {
                    // Pega aquí el DSN que sacaste de Client Keys en Sentry
                    options.Dsn = "https://90b22ba348971e816b3e9fc785f994bb@o4511299157884928.ingest.us.sentry.io/4511299207168000";

                    // Esto ayuda a capturar errores de rendimiento y logs de sistema
                    options.TracesSampleRate = 1.0;

                    // Si estás en desarrollo en la oficina, deja esto en true
                    options.Debug = true;

                    // Captura errores cuando la Zebra se queda sin red
                    options.AutoSessionTracking = true;
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "SelderData.db3");

            // Registramos la base de datos como un "Singleton"
            builder.Services.AddSingleton(s => new SQLiteAsyncConnection(dbPath));

            // Registramos la página para que MAUI sepa cómo crearla
            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}