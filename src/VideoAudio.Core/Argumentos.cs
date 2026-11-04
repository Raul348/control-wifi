using System;
using System.Globalization;
using System.Text;

namespace VideoAudio.Core
{
    /// <summary>Arma las líneas de comando de FFmpeg y de whisper.cpp.</summary>
    public static class Argumentos
    {
        /// <summary>
        /// Extrae el audio del video. -vn quita la imagen; -n nunca sobreescribe (el programa ya elige un nombre libre);
        /// -progress pipe:1 escribe el avance en la salida estándar para mostrar la barra.
        /// </summary>
        public static string Audio(string video, string salida, Formato formato, int kbps)
        {
            var sb = new StringBuilder("-hide_banner -nostdin -n -i ");
            sb.Append(Comillas(video)).Append(" -vn -sn -dn -map 0:a:0 ");
            switch (formato)
            {
                case Formato.Wav:
                    sb.Append("-c:a pcm_s16le ");
                    break;
                case Formato.M4a:
                    sb.Append("-c:a aac -b:a ").Append(kbps.ToString(CultureInfo.InvariantCulture)).Append("k ");
                    break;
                default:
                    sb.Append("-c:a libmp3lame -b:a ").Append(kbps.ToString(CultureInfo.InvariantCulture)).Append("k ");
                    break;
            }
            sb.Append("-progress pipe:1 -nostats ").Append(Comillas(salida));
            return sb.ToString();
        }

        /// <summary>Audio para Whisper: WAV a 16 kHz, un canal, 16 bits. Es el único formato que acepta whisper.cpp.</summary>
        public static string WavParaWhisper(string video, string salida)
            => "-hide_banner -nostdin -y -i " + Comillas(video) + " -vn -sn -dn -map 0:a:0 -ar 16000 -ac 1 -c:a pcm_s16le -progress pipe:1 -nostats " + Comillas(salida);

        /// <summary>Solo lee la información del archivo (duración y pistas).</summary>
        public static string Informacion(string archivo) => "-hide_banner -nostdin -i " + Comillas(archivo);

        /// <summary>
        /// Transcripción con whisper-cli. Escribe base.txt y base.srt.
        /// -pp muestra el avance; -t usa varios núcleos del procesador.
        /// </summary>
        public static string Whisper(string modelo, string wav, string baseSalida, string idioma, int hilos)
            => "-m " + Comillas(modelo) + " -f " + Comillas(wav) + " -l " + idioma +
               " -t " + Math.Max(1, hilos).ToString(CultureInfo.InvariantCulture) +
               " -pp -otxt -osrt -of " + Comillas(baseSalida);

        /// <summary>Pone comillas a un argumento de Windows, escapando las comillas y barras finales.</summary>
        public static string Comillas(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            int barras = 0;
            foreach (char c in s)
            {
                if (c == '\\') { barras++; continue; }
                if (c == '"') { sb.Append('\\', barras * 2 + 1).Append('"'); barras = 0; continue; }
                sb.Append('\\', barras).Append(c);
                barras = 0;
            }
            sb.Append('\\', barras * 2).Append('"');
            return sb.ToString();
        }
    }
}
