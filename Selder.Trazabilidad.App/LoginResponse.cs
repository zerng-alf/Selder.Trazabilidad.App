using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Selder.Trazabilidad.App.Models // Ajusta el namespace a tu proyecto
{
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Nombre { get; set; }
    }
}
