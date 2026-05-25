namespace Selder.Trazabilidad.App.Models;

public class Usuario
{
    public int PNCODUSUARIO { get; set; }
    public string PSUSUARIO { get; set; }
    public int? IDAREA { get; set; }
    public string DSNOMBRE { get; set; }
    public string DSCONTRASENIA { get; set; }
    public int? DNNIVEL { get; set; }
    public string CODIGOBARRAS { get; set; }
    public string ESTATUS { get; set; }
}