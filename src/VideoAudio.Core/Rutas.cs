using System;
using System.IO;

namespace VideoAudio.Core
{
    public static class Rutas
    {
        /// <summary>
        /// Nombre de salida que no existe todavía: "clase.mp3", "clase (2).mp3", "clase (3).mp3"...
        /// Nunca devuelve el mismo archivo de entrada, así el video original no se puede borrar.
        /// </summary>
        public static string Libre(string carpeta, string nombreBase, string extension, Func<string, bool> existe, string entrada = null)
        {
            for (int n = 1; n < 10000; n++)
            {
                string nombre = n == 1 ? nombreBase + extension : nombreBase + " (" + n + ")" + extension;
                string ruta = Path.Combine(carpeta, nombre);
                if (entrada != null && string.Equals(Path.GetFullPath(ruta), Path.GetFullPath(entrada), StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!existe(ruta)) return ruta;
            }
            throw new IOException("No se encontró un nombre libre para " + nombreBase + extension);
        }

        /// <summary>Extensiones de video y audio que FFmpeg sabe leer y que el programa acepta al arrastrar.</summary>
        public static readonly string[] Extensiones =
        {
            ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".ts", ".mts", ".m2ts", ".vob", ".ogv",
            ".mp3", ".m4a", ".wav", ".aac", ".ogg", ".opus", ".flac", ".wma"
        };

        public static bool Aceptado(string ruta)
            => Array.IndexOf(Extensiones, (Path.GetExtension(ruta) ?? "").ToLowerInvariant()) >= 0;
    }
}
