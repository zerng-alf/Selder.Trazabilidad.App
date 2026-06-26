using SQLite;

namespace Selder.Trazabilidad.App.Models;

public class MovimientoLocal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string NumLote { get; set; }

    public string Etapa { get; set; }

    public DateTime Fecha { get; set; }

    public bool Sincronizado { get; set; }

}