using SQLite;
using Selder.Trazabilidad.App.Services;
using Selder.Trazabilidad.App.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentry;

namespace Selder.Trazabilidad.App
{
    public partial class App : Application
    {
        public static SQLiteAsyncConnection Database { get; private set; }
        public static IServiceProvider Services { get; private set; }
       
        // VARIABLE GLOBAL: Aquí guardaremos el ID del operador (ej: "1130")
        public static string UsuarioLogueadoId { get; set; } = "ANÓNIMO";

        public App(SQLiteAsyncConnection db, IServiceProvider services)
        {
            InitializeComponent();
            Database = db;
            Services = services;
            MainPage = new NavigationPage(new LoginPage(Services.GetRequiredService<IAuthService>()));
            IniciarSincronizacionAutomatica(db);
        }

        private async void IniciarSincronizacionAutomatica(SQLiteAsyncConnection db)
        {
            try
            {
                // Delay inicial para no bloquear el inicio de la UI
                await Task.Delay(AppConfig.SyncInitialDelayMs);

                var service = new TraceabilityService(db);
                var (enviados, fallidos) = await service.SyncPendientesAsync();

                if (enviados > 0)
                {
                    SentrySdk.CaptureMessage($"Sincronizados {enviados} registros offline al iniciar");
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
        }
    }
}