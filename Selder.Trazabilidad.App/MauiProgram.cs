using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SQLite;
using Sentry;
using Selder.Trazabilidad.App.Services;

namespace Selder.Trazabilidad.App
{
    public static class MauiProgram
    {
        public static IServiceProvider Services { get; private set; }
        
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseSentry(options =>
                {
                    options.Dsn = "https://90b22ba348971e816b3e9fc785f994bb@o4511299157884928.ingest.us.sentry.io/4511299207168000";
                    options.TracesSampleRate = 1.0;
                    options.Debug = true;
                    options.AutoSessionTracking = true;
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "SelderData.db3");
            var db = new SQLiteAsyncConnection(dbPath);
            db.CreateTableAsync<Models.MovimientoLocal>().Wait();

            builder.Services.AddSingleton(db);
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<MainPage>();

            var mauiApp = builder.Build();
            Services = mauiApp.Services;

            return mauiApp;
        }
    }
}