using System;
using System.Collections.Generic;
using System.IO;
using VideoAudio.Core;
using Xunit;

namespace VideoAudio.Tests
{
    public class PruebasCore
    {
        [Fact]
        public void Lee_la_duracion_de_ffmpeg()
        {
            Assert.Equal(TimeSpan.FromSeconds(9 * 60 + 12.34), Progreso.LeerDuracion("  Duration: 00:09:12.34, start: 0.000000, bitrate: 1520 kb/s"));
            Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(5), Progreso.LeerDuracion("Duration: 01:00:05.00, start"));
            Assert.Null(Progreso.LeerDuracion("Stream #0:0: Video: h264"));
            Assert.Null(Progreso.LeerDuracion("  Duration: N/A, bitrate: N/A"));
        }

        [Fact]
        public void Lee_el_avance_de_ffmpeg()
        {
            Assert.Equal(TimeSpan.FromSeconds(62.5), Progreso.LeerAvance("out_time_us=62500000"));
            Assert.Equal(TimeSpan.FromSeconds(62.5), Progreso.LeerAvance("out_time_ms=62500000"));
            Assert.Equal(TimeSpan.FromSeconds(62.5), Progreso.LeerAvance("out_time=00:01:02.500000"));
            Assert.Null(Progreso.LeerAvance("out_time_us=N/A"));
            Assert.Null(Progreso.LeerAvance("progress=continue"));
        }

        [Fact]
        public void Lee_el_avance_de_whisper()
        {
            Assert.Equal(45, Progreso.LeerWhisper("whisper_print_progress_callback: progress =  45%"));
            Assert.Equal(100, Progreso.LeerWhisper("whisper_print_progress_callback: progress = 100%"));
            Assert.Null(Progreso.LeerWhisper("whisper_init_from_file_with_params_no_state: loading model"));
        }

        [Fact]
        public void Audio_completo_o_cortado()
        {
            Assert.True(Progreso.Completo(TimeSpan.FromMinutes(9), TimeSpan.FromSeconds(9 * 60 - 1)));
            Assert.False(Progreso.Completo(TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(1)));
            Assert.Equal("9:05", Progreso.Texto(TimeSpan.FromSeconds(545)));
            Assert.Equal("1:02:03", Progreso.Texto(new TimeSpan(1, 2, 3)));
        }

        [Fact]
        public void Nombre_libre_nunca_pisa_archivos_ni_el_video()
        {
            var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine("C:", "v", "clase.mp3"),
                Path.Combine("C:", "v", "clase (2).mp3"),
            };
            string r = Rutas.Libre(Path.Combine("C:", "v"), "clase", ".mp3", existentes.Contains);
            Assert.Equal(Path.Combine("C:", "v", "clase (3).mp3"), r);

            // Si el "audio" coincidiera con la entrada (por ejemplo un .wav convertido a .wav), se salta ese nombre.
            string entrada = Path.Combine("C:", "v", "voz.wav");
            Assert.Equal(Path.Combine("C:", "v", "voz (2).wav"), Rutas.Libre(Path.Combine("C:", "v"), "voz", ".wav", _ => false, entrada));
        }

        [Fact]
        public void Argumentos_de_ffmpeg()
        {
            string a = Argumentos.Audio(@"C:\Vídeos\Clase 1.mp4", @"C:\Vídeos\Clase 1.mp3", Formato.Mp3, 192);
            Assert.Contains("-n ", a);                                  // nunca sobreescribe
            Assert.Contains("-i \"C:\\Vídeos\\Clase 1.mp4\"", a);
            Assert.Contains("-vn", a);
            Assert.Contains("-c:a libmp3lame -b:a 192k", a);
            Assert.EndsWith("\"C:\\Vídeos\\Clase 1.mp3\"", a);
            Assert.Contains("-c:a pcm_s16le", Argumentos.Audio("a.mp4", "a.wav", Formato.Wav, 192));
            Assert.Contains("-c:a aac -b:a 256k", Argumentos.Audio("a.mp4", "a.m4a", Formato.M4a, 256));
            Assert.Contains("-ar 16000 -ac 1", Argumentos.WavParaWhisper("a.mp4", "a.wav"));
        }

        [Fact]
        public void Argumentos_de_whisper()
        {
            string a = Argumentos.Whisper(@"C:\m\ggml-small.bin", @"C:\t\a.wav", @"C:\t\a", "es", 8);
            Assert.Equal("-m \"C:\\m\\ggml-small.bin\" -f \"C:\\t\\a.wav\" -l es -t 8 -pp -otxt -osrt -of \"C:\\t\\a\"", a);
        }

        [Theory]
        [InlineData("simple", "\"simple\"")]
        [InlineData(@"C:\con espacio\a.mp4", "\"C:\\con espacio\\a.mp4\"")]
        [InlineData(@"C:\carpeta\", "\"C:\\carpeta\\\\\"")]
        [InlineData("dice \"hola\"", "\"dice \\\"hola\\\"\"")]
        public void Comillas_de_windows(string entrada, string esperado)
        {
            Assert.Equal(esperado, Argumentos.Comillas(entrada));
        }

        [Fact]
        public void Extensiones_aceptadas()
        {
            Assert.True(Rutas.Aceptado(@"C:\a\clase.MP4"));
            Assert.True(Rutas.Aceptado("x.mkv"));
            Assert.False(Rutas.Aceptado("x.docx"));
        }
    }
}
