using Microsoft.Extensions.Logging;
using SQLite;

namespace Selder.Trazabilidad.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // --- AGREGA ESTO SI NO ESTÁ ---
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "SelderData.db3");

            // Registramos la base de datos como un "Singleton"
            builder.Services.AddSingleton(s => new SQLiteAsyncConnection(dbPath));

            // Registramos la página para que MAUI sepa cómo crearla
            builder.Services.AddSingleton<MainPage>();
            // ------------------------------

            return builder.Build();
        }
    }
}
