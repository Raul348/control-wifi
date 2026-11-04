using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VideoAudio.Core
{
    /// <summary>Lee la duración y el avance desde lo que escriben FFmpeg y whisper.cpp.</summary>
    public static class Progreso
    {
        private static readonly Regex Duracion = new Regex(@"Duration:\s*(\d+):(\d{2}):(\d{2}(?:\.\d+)?)", RegexOptions.Compiled);
        private static readonly Regex Whisper = new Regex(@"progress\s*=\s*(\d+)\s*%", RegexOptions.Compiled);

        /// <summary>"  Duration: 00:09:12.34, start: ..." → 9 min 12,34 s. Null si la línea no la trae.</summary>
        public static TimeSpan? LeerDuracion(string linea)
        {
            if (string.IsNullOrEmpty(linea)) return null;
            Match m = Duracion.Match(linea);
            if (!m.Success) return null;
            double s = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 +
                       double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return TimeSpan.FromSeconds(s);
        }

        /// <summary>Línea de -progress: "out_time_us=12345678" o "out_time_ms=12345678" (ambos en microsegundos) o "out_time=00:01:02.5".</summary>
        public static TimeSpan? LeerAvance(string linea)
        {
            if (string.IsNullOrEmpty(linea)) return null;
            int i = linea.IndexOf('=');
            if (i < 0) return null;
            string clave = linea.Substring(0, i).Trim(), valor = linea.Substring(i + 1).Trim();
            if ((clave == "out_time_us" || clave == "out_time_ms") && long.TryParse(valor, out long us) && us >= 0)
                return TimeSpan.FromTicks(us * 10);
            if (clave == "out_time")
            {
                TimeSpan? t = LeerDuracion("Duration: " + valor);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>"whisper_print_progress_callback: progress =  45%" → 45.</summary>
        public static int? LeerWhisper(string linea)
        {
            if (string.IsNullOrEmpty(linea)) return null;
            Match m = Whisper.Match(linea);
            return m.Success ? int.Parse(m.Groups[1].Value) : (int?)null;
        }

        /// <summary>Verdadero si la salida de FFmpeg dice que el archivo no tiene pista de audio.</summary>
        public static bool SinAudio(string salidaFfmpeg)
        {
            if (string.IsNullOrEmpty(salidaFfmpeg)) return false;
            return salidaFfmpeg.Contains("matches no streams") ||
                   salidaFfmpeg.Contains("does not contain any stream") ||
                   salidaFfmpeg.Contains("Output file does not contain any stream");
        }

        /// <summary>
        /// Verdadero si FFmpeg se negó a escribir porque el destino ya existe.
        /// Ojo: en ese caso FFmpeg termina con código 0, así que no basta con mirar el código de salida.
        /// </summary>
        public static bool DestinoExistente(string salidaFfmpeg)
            => !string.IsNullOrEmpty(salidaFfmpeg) && salidaFfmpeg.Contains("already exists. Exiting");

        /// <summary>
        /// La conversión salió bien solo si: FFmpeg terminó con 0, no se negó a escribir, no faltaba el audio,
        /// y el archivo de salida existe y no está vacío.
        /// </summary>
        public static bool Exito(int codigo, string errorFfmpeg, bool salidaExiste, long bytesSalida)
            => codigo == 0 && !DestinoExistente(errorFfmpeg) && !SinAudio(errorFfmpeg) && salidaExiste && bytesSalida > 0;

        public static string Texto(TimeSpan t)
            => t.TotalHours >= 1 ? ((int)t.TotalHours) + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00")
                                 : t.Minutes + ":" + t.Seconds.ToString("00");

        /// <summary>El audio está completo si dura al menos el 98 % del video (o le falta menos de 2 s).</summary>
        public static bool Completo(TimeSpan video, TimeSpan audio)
            => video.TotalSeconds <= 0 || audio.TotalSeconds >= video.TotalSeconds * 0.98 || video.TotalSeconds - audio.TotalSeconds < 2;
    }
}
