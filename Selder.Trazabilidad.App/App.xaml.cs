using SQLite;

namespace Selder.Trazabilidad.App
{
    public partial class App : Application
    {
        // Creamos una propiedad estática para acceder a la BD desde cualquier pantalla
        public static SQLiteAsyncConnection Database { get; private set; }

        public App(SQLiteAsyncConnection db)
        {
            InitializeComponent();

            Database = db; // Guardamos la conexión aquí

            // Arrancamos directo en el Login
            MainPage = new NavigationPage(new LoginPage());
        }
    }
}


