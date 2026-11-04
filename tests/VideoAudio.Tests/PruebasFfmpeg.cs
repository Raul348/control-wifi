using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using VideoAudio.Core;
using Xunit;

namespace VideoAudio.Tests
{
    /// <summary>
    /// Pruebas con FFmpeg real. Se omiten si FFmpeg no está instalado.
    /// Crean un video de prueba, le sacan el audio con los mismos argumentos del programa y comprueban la duración.
    /// </summary>
    public class PruebasFfmpeg
    {
        private static string Ffmpeg()
        {
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                foreach (string n in new[] { "ffmpeg", "ffmpeg.exe" })
                {
                    string r = Path.Combine(dir, n);
                    if (File.Exists(r)) return r;
                }
            }
            return null;
        }

        private static (int Codigo, string Salida, string Error) Ejecutar(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi)!;
            var so = p.StandardOutput.ReadToEndAsync();
            var se = p.StandardError.ReadToEndAsync();
            p.WaitForExit();
            return (p.ExitCode, so.Result, se.Result);
        }

        private static string Carpeta()
        {
            string d = Path.Combine(Path.GetTempPath(), "videoaudio-pruebas-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        [Fact]
        public void Extrae_el_audio_completo_de_un_video()
        {
            string ff = Ffmpeg();
            if (ff == null) return;
            string d = Carpeta();
            string video = Path.Combine(d, "Clase ñandú 1.mp4");
            Ejecutar(ff, "-hide_banner -loglevel error -y -f lavfi -i testsrc=size=160x120:rate=10 -f lavfi -i sine=frequency=440 -t 12 -c:v libx264 -c:a aac -shortest " + Argumentos.Comillas(video));
            Assert.True(File.Exists(video));

            foreach (Formato f in new[] { Formato.Mp3, Formato.M4a, Formato.Wav })
            {
                string salida = Rutas.Libre(d, "Clase ñandú 1", Formatos.Extension(f), File.Exists, video);
                var r = Ejecutar(ff, Argumentos.Audio(video, salida, f, 192));
                Assert.True(Progreso.Exito(r.Codigo, r.Error, File.Exists(salida), File.Exists(salida) ? new FileInfo(salida).Length : 0), r.Error);

                // El avance llega por la salida estándar (-progress pipe:1) y termina con progress=end.
                Assert.Contains("progress=end", r.Salida);
                TimeSpan? ultimo = null;
                foreach (string l in r.Salida.Split('\n')) { var t = Progreso.LeerAvance(l); if (t != null) ultimo = t; }
                Assert.NotNull(ultimo);

                // La duración del video y la del audio coinciden.
                TimeSpan? dv = Progreso.LeerDuracion(Ejecutar(ff, Argumentos.Informacion(video)).Error);
                TimeSpan? da = Progreso.LeerDuracion(Ejecutar(ff, Argumentos.Informacion(salida)).Error);
                Assert.NotNull(dv);
                Assert.NotNull(da);
                Assert.True(Progreso.Completo(dv!.Value, da!.Value), $"{f}: video {dv} audio {da}");
            }

            // Una segunda conversión no pisa la primera: usa "(2)".
            string otra = Rutas.Libre(d, "Clase ñandú 1", ".mp3", File.Exists, video);
            Assert.EndsWith("Clase ñandú 1 (2).mp3", otra);
            Directory.Delete(d, true);
        }

        [Fact]
        public void Avisa_cuando_el_video_no_tiene_audio()
        {
            string ff = Ffmpeg();
            if (ff == null) return;
            string d = Carpeta();
            string video = Path.Combine(d, "mudo.mp4");
            Ejecutar(ff, "-hide_banner -loglevel error -y -f lavfi -i testsrc=size=160x120:rate=10 -t 2 -c:v libx264 " + Argumentos.Comillas(video));
            string salida = Path.Combine(d, "mudo.mp3");
            var r = Ejecutar(ff, Argumentos.Audio(video, salida, Formato.Mp3, 192));
            Assert.True(Progreso.SinAudio(r.Error), r.Error);
            Assert.False(Progreso.Exito(r.Codigo, r.Error, File.Exists(salida), File.Exists(salida) ? new FileInfo(salida).Length : 0));
            Directory.Delete(d, true);
        }

        [Fact]
        public void Wav_para_whisper_es_16khz_mono()
        {
            string ff = Ffmpeg();
            if (ff == null) return;
            string d = Carpeta();
            string video = Path.Combine(d, "v.mp4");
            Ejecutar(ff, "-hide_banner -loglevel error -y -f lavfi -i testsrc=size=160x120:rate=10 -f lavfi -i sine=frequency=440 -t 3 -c:v libx264 -c:a aac -shortest " + Argumentos.Comillas(video));
            string wav = Path.Combine(d, "w.wav");
            Assert.Equal(0, Ejecutar(ff, Argumentos.WavParaWhisper(video, wav)).Codigo);
            string info = Ejecutar(ff, Argumentos.Informacion(wav)).Error;
            Assert.Contains("16000 Hz", info);
            Assert.Contains("mono", info);
            Assert.Contains("pcm_s16le", info);
            Directory.Delete(d, true);
        }

        [Fact]
        public void Nunca_sobreescribe_aunque_se_le_pase_un_archivo_existente()
        {
            string ff = Ffmpeg();
            if (ff == null) return;
            string d = Carpeta();
            string video = Path.Combine(d, "v.mp4");
            Ejecutar(ff, "-hide_banner -loglevel error -y -f lavfi -i testsrc=size=160x120:rate=10 -f lavfi -i sine=frequency=440 -t 2 -c:v libx264 -c:a aac -shortest " + Argumentos.Comillas(video));
            long antes = new FileInfo(video).Length;
            // Aun si por error el destino fuera el propio video, -n hace que FFmpeg se niegue y el video no cambia.
            var r = Ejecutar(ff, Argumentos.Audio(video, video, Formato.Mp3, 192));
            Assert.Equal(antes, new FileInfo(video).Length);
            // FFmpeg devuelve 0 aunque se negó: el programa lo detecta por el mensaje.
            Assert.True(Progreso.DestinoExistente(r.Error), r.Error);
            Assert.False(Progreso.Exito(r.Codigo, r.Error, true, antes));
            Directory.Delete(d, true);
        }
    }
}
