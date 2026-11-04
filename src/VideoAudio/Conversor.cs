using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VideoAudio.Core;

namespace VideoAudio
{
    /// <summary>Resultado de procesar un archivo.</summary>
    internal sealed class Resultado
    {
        public bool Ok;
        public string Mensaje = "";
        public string Audio;
        public TimeSpan? DuracionVideo;
        public TimeSpan? DuracionAudio;
    }

    /// <summary>Ejecuta FFmpeg y whisper.cpp para un archivo.</summary>
    internal sealed class Conversor
    {
        public string Ffmpeg;
        public string Whisper;
        public string Modelo;

        /// <summary>Saca el audio. progreso: 0 a 100.</summary>
        public async Task<Resultado> Convertir(string video, string carpetaSalida, Formato formato, int kbps,
            IProgress<double> progreso, CancellationToken ct)
        {
            var r = new Resultado();
            string carpeta = string.IsNullOrEmpty(carpetaSalida) ? Path.GetDirectoryName(video) : carpetaSalida;
            string nombre = Path.GetFileNameWithoutExtension(video);
            string salida = Rutas.Libre(carpeta, nombre, Formatos.Extension(formato), File.Exists, video);

            TimeSpan? duracion = null;
            (int Codigo, string Salida, string Error) ejecucion;
            try
            {
                ejecucion = await Ejecutar(Ffmpeg, Argumentos.Audio(video, salida, formato, kbps),
                    linea =>
                    {
                        TimeSpan? t = Progreso.LeerAvance(linea);
                        if (t != null && duracion != null && duracion.Value.TotalSeconds > 0)
                            progreso?.Report(Math.Min(100, t.Value.TotalSeconds * 100 / duracion.Value.TotalSeconds));
                    },
                    linea => { if (duracion == null) duracion = Progreso.LeerDuracion(linea); },
                    ct);
            }
            catch (OperationCanceledException)
            {
                // El nombre de salida era nuevo (Rutas.Libre), así que borrar el audio a medias no toca nada del usuario.
                TryDelete(salida);
                throw;
            }

            bool existe = File.Exists(salida);
            long bytes = existe ? new FileInfo(salida).Length : 0;
            if (!Progreso.Exito(ejecucion.Codigo, ejecucion.Error, existe, bytes))
            {
                if (existe && !Progreso.DestinoExistente(ejecucion.Error)) TryDelete(salida);
                r.Mensaje = Progreso.SinAudio(ejecucion.Error) ? "El video no tiene audio."
                          : Progreso.DestinoExistente(ejecucion.Error) ? "El archivo de salida ya existía; no se tocó."
                          : "FFmpeg no pudo convertir: " + UltimaLinea(ejecucion.Error);
                return r;
            }

            // Verificación: el audio debe durar lo mismo que el video (lo que falló con VLC).
            r.DuracionVideo = duracion;
            r.DuracionAudio = await Duracion(salida, ct);
            r.Audio = salida;
            r.Ok = true;
            if (duracion != null && r.DuracionAudio != null && !Progreso.Completo(duracion.Value, r.DuracionAudio.Value))
                r.Mensaje = "Atención: el audio dura " + Progreso.Texto(r.DuracionAudio.Value) + " y el video " + Progreso.Texto(duracion.Value) + ".";
            progreso?.Report(100);
            return r;
        }

        /// <summary>
        /// Transcribe con Whisper y deja .txt y .srt junto al audio.
        /// Trabaja en una carpeta temporal con nombres simples, porque whisper.cpp en Windows falla con tildes o ñ en la ruta.
        /// </summary>
        public async Task<(bool Ok, string Mensaje, string Txt, string Srt)> Transcribir(string video, string carpetaSalida, string idioma,
            IProgress<double> progreso, CancellationToken ct)
        {
            string carpeta = string.IsNullOrEmpty(carpetaSalida) ? Path.GetDirectoryName(video) : carpetaSalida;
            string nombre = Path.GetFileNameWithoutExtension(video);
            string tmp = Path.Combine(Path.GetTempPath(), "VideoAudio-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                string wav = Path.Combine(tmp, "audio.wav");
                TimeSpan? duracion = null;
                var e1 = await Ejecutar(Ffmpeg, Argumentos.WavParaWhisper(video, wav),
                    linea =>
                    {
                        TimeSpan? t = Progreso.LeerAvance(linea);
                        if (t != null && duracion != null && duracion.Value.TotalSeconds > 0)
                            progreso?.Report(Math.Min(10, t.Value.TotalSeconds * 10 / duracion.Value.TotalSeconds));
                    },
                    linea => { if (duracion == null) duracion = Progreso.LeerDuracion(linea); },
                    ct);
                if (!File.Exists(wav) || new FileInfo(wav).Length == 0)
                    return (false, Progreso.SinAudio(e1.Error) ? "El video no tiene audio." : "No se pudo preparar el audio: " + UltimaLinea(e1.Error), null, null);

                string baseSalida = Path.Combine(Corta(tmp), "texto");
                var e2 = await Ejecutar(Whisper, Argumentos.Whisper(Corta(Modelo), Corta(wav), baseSalida, idioma, Environment.ProcessorCount),
                    linea => Avance(linea, progreso),
                    linea => Avance(linea, progreso),
                    ct, Path.GetDirectoryName(Whisper));

                string txt = Path.Combine(tmp, "texto.txt"), srt = Path.Combine(tmp, "texto.srt");
                if (!File.Exists(txt))
                    return (false, "Whisper no pudo transcribir: " + UltimaLinea(e2.Error + "\n" + e2.Salida), null, null);

                string destinoTxt = Rutas.Libre(carpeta, nombre, ".txt", File.Exists, video);
                File.Copy(txt, destinoTxt);
                string destinoSrt = null;
                if (File.Exists(srt))
                {
                    destinoSrt = Rutas.Libre(carpeta, nombre, ".srt", File.Exists, video);
                    File.Copy(srt, destinoSrt);
                }
                progreso?.Report(100);
                return (true, "", destinoTxt, destinoSrt);
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch { /* temporal */ }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetShortPathName(string larga, StringBuilder corta, uint tam);

        /// <summary>
        /// Ruta corta de Windows (8.3), siempre sin tildes ni ñ, para programas que no aceptan esos caracteres.
        /// Si la ruta ya es simple, o Windows no tiene rutas cortas en ese disco, devuelve la misma.
        /// </summary>
        private static string Corta(string ruta)
        {
            if (ruta.All(c => c < 128)) return ruta;
            try
            {
                var sb = new StringBuilder(1024);
                uint n = GetShortPathName(ruta, sb, (uint)sb.Capacity);
                return n > 0 && n < sb.Capacity ? sb.ToString() : ruta;
            }
            catch { return ruta; }
        }

        private static void Avance(string linea, IProgress<double> progreso)
        {
            int? p = Progreso.LeerWhisper(linea);
            if (p != null) progreso?.Report(10 + p.Value * 0.9);
        }

        public async Task<TimeSpan?> Duracion(string archivo, CancellationToken ct)
        {
            var e = await Ejecutar(Ffmpeg, Argumentos.Informacion(archivo), null, null, ct);
            return Progreso.LeerDuracion(e.Error);
        }

        private static string UltimaLinea(string texto)
        {
            string[] l = (texto ?? "").Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return l.Length == 0 ? "" : l[l.Length - 1].Trim();
        }

        private static void TryDelete(string ruta) { try { File.Delete(ruta); } catch { /* nada */ } }

        /// <summary>Ejecuta un programa de consola, leyendo su salida línea por línea. Se detiene si se cancela.</summary>
        private static async Task<(int Codigo, string Salida, string Error)> Ejecutar(string exe, string args,
            Action<string> alSalir, Action<string> alError, CancellationToken ct, string carpetaTrabajo = null)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            if (carpetaTrabajo != null) psi.WorkingDirectory = carpetaTrabajo;

            var salida = new StringBuilder();
            var error = new StringBuilder();
            var fin = new TaskCompletionSource<int>();
            using (var p = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                p.OutputDataReceived += (s, e) => { if (e.Data == null) return; lock (salida) salida.AppendLine(e.Data); alSalir?.Invoke(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data == null) return; lock (error) error.AppendLine(e.Data); alError?.Invoke(e.Data); };
                p.Exited += (s, e) => fin.TrySetResult(0);
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                using (ct.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { /* ya terminó */ } }))
                {
                    await fin.Task.ConfigureAwait(false);
                    p.WaitForExit();   // vacía las últimas líneas de salida
                }
                ct.ThrowIfCancellationRequested();
                return (p.ExitCode, salida.ToString(), error.ToString());
            }
        }
    }
}
