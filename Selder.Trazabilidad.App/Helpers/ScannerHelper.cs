using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;


namespace Selder.Trazabilidad.App.Helpers
{
    public static class ScannerHelper
    {
        private static CancellationTokenSource _cts = new CancellationTokenSource();

        public static async Task<string> ValidarCodigoCompleto(string textoNuevo, int delayMs = 800) // Subimos a 600ms
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                await Task.Delay(delayMs, _cts.Token);
                // Retornamos el texto capturado después de la espera
                return textoNuevo?.Trim().ToUpper() ?? "";
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }

        public static void ForzarFoco(Entry entry)
        {
            if (entry == null) return;

            entry.Unfocused += (s, e) =>
            {
                Task.Run(async () =>
                {
                    await Task.Delay(150);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!entry.IsFocused) entry.Focus();
                    });
                });
            };
        }
    }
}