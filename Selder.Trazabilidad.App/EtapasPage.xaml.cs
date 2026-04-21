using SQLite;

namespace Selder.Trazabilidad.App;

public partial class EtapasPage : ContentPage
{
    string loteCapturado;
    // Usamos la conexión global de App
    private readonly SQLiteAsyncConnection _db = App.Database;

    public EtapasPage(string lote)
    {
        InitializeComponent();
        loteCapturado = lote;
        LblLoteInfo.Text = $"LOTE: {lote}";

        // Importante: Poner el foco para recibir el escáner
        TxtScannerEtapa.Focus();
    }

    private async void OnScannerEtapaChanged(object sender, TextChangedEventArgs e)
    {
        string codigo = e.NewTextValue?.Trim().ToUpper();
        if (string.IsNullOrEmpty(codigo)) return;

        // Limpiar para el siguiente escaneo
        TxtScannerEtapa.Text = string.Empty;

        // Lógica de escaneo para tiempos
        if (codigo == "INICIO")
        {
            await RegistrarEvento("INICIO");
            await DisplayAlert("Éxito", "Inicio de etapa registrado", "OK");
        }
        else if (codigo == "FIN")
        {
            await RegistrarEvento("FIN");
            await DisplayAlert("Éxito", "Fin de etapa registrado", "OK");

            // Regresamos a la pantalla de lotes
            await Navigation.PopAsync();
        }
    }

    private async Task RegistrarEvento(string accion)
    {
        if (_db == null) return;

        await _db.InsertAsync(new MovimientoLocal
        {
            NumLote = loteCapturado,
            Etapa = $"PESADO_{accion}", // Aquí puedes cambiar la etapa si es Secado, etc.
            Fecha = DateTime.Now,
            Sincronizado = false
        });
    }
}