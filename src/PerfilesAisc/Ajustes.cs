using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PerfilesAisc
{
    /// <summary>Recuerda los últimos valores usados, en %AppData%\ACERORefuerzo\perfiles_aisc.txt (clave=valor por línea).</summary>
    internal static class Ajustes
    {
        private static readonly string Ruta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ACERORefuerzo", "perfiles_aisc.txt");

        private static Dictionary<string, string> _datos;

        private static Dictionary<string, string> Datos()
        {
            if (_datos != null) return _datos;
            _datos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(Ruta))
                    foreach (string linea in File.ReadAllLines(Ruta))
                    {
                        int i = linea.IndexOf('=');
                        if (i > 0) _datos[linea.Substring(0, i).Trim()] = linea.Substring(i + 1).Trim();
                    }
            }
            catch { /* sin ajustes guardados */ }
            return _datos;
        }

        public static string Leer(string clave, string porDefecto)
            => Datos().TryGetValue(clave, out string v) ? v : porDefecto;

        public static double Leer(string clave, double porDefecto)
            => double.TryParse(Leer(clave, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : porDefecto;

        public static int Leer(string clave, int porDefecto)
            => int.TryParse(Leer(clave, ""), out int v) ? v : porDefecto;

        public static bool Leer(string clave, bool porDefecto)
            => bool.TryParse(Leer(clave, ""), out bool v) ? v : porDefecto;

        public static void Guardar(string clave, object valor)
        {
            Datos()[clave] = valor is double d ? d.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(valor, CultureInfo.InvariantCulture);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Ruta));
                var lineas = new List<string>();
                foreach (var kv in Datos()) lineas.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(Ruta, lineas);
            }
            catch { /* no importa si no se puede guardar */ }
        }
    }
}
