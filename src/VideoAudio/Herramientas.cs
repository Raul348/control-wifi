using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoAudio.Core;

namespace VideoAudio
{
    /// <summary>
    /// Ubica y descarga FFmpeg, whisper.cpp y los modelos de Whisper.
    /// Se guardan en %LocalAppData%\VideoAudio para no tener que descargarlos otra vez.
    /// También sirven si se copian a mano junto al programa.
    /// </summary>
    internal static class Herramientas
    {
        public static readonly string Carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoAudio");

        private static string CarpetaPrograma => AppDomain.CurrentDomain.BaseDirectory;

        // FFmpeg para Windows: primero gyan.dev (versión estable, la más usada); si falla, BtbN en GitHub.
        private static readonly string[] UrlsFfmpeg =
        {
            "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
        };

        // whisper.cpp: se busca el archivo whisper-bin-x64.zip en la última versión publicada.
        private const string ApiWhisper = "https://api.github.com/repos/ggml-org/whisper.cpp/releases/latest";
        private const string UrlWhisperFija = "https://github.com/ggml-org/whisper.cpp/releases/latest/download/whisper-bin-x64.zip";

        // ------------------------------------------------------------------ ubicar

        public static string Ffmpeg() => Buscar("ffmpeg.exe");

        public static string Whisper() => Buscar("whisper-cli.exe") ?? Buscar("main.exe", subcarpeta: "whisper");

        public static string Modelo(ModeloWhisper m)
        {
            foreach (string dir in new[] { Path.Combine(Carpeta, "modelos"), CarpetaPrograma, Path.Combine(CarpetaPrograma, "modelos") })
            {
                string r = Path.Combine(dir, m.Archivo);
                if (File.Exists(r) && new FileInfo(r).Length > 1_000_000) return r;
            }
            return null;
        }

        /// <summary>Busca junto al programa, en la carpeta de herramientas y en el PATH de Windows.</summary>
        private static string Buscar(string exe, string subcarpeta = null)
        {
            var dirs = new[]
            {
                CarpetaPrograma,
                Path.Combine(CarpetaPrograma, "ffmpeg"),
                Path.Combine(CarpetaPrograma, "whisper"),
                Path.Combine(Carpeta, "ffmpeg"),
                Path.Combine(Carpeta, "whisper"),
            }.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));

            foreach (string d in dirs)
            {
                if (string.IsNullOrWhiteSpace(d)) continue;
                if (subcarpeta != null && !d.TrimEnd('\\', '/').EndsWith(subcarpeta, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    string r = Path.Combine(d.Trim(), exe);
                    if (File.Exists(r)) return r;
                }
                catch { /* entrada del PATH inválida */ }
            }
            return null;
        }

        // ------------------------------------------------------------------ descargar

        private static HttpClient Cliente()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("VideoAudio/1.0 (+Windows)");
            return c;
        }

        /// <summary>Descarga FFmpeg y deja ffmpeg.exe en la carpeta de herramientas.</summary>
        public static async Task<string> DescargarFfmpeg(IProgress<(long, long)> progreso, CancellationToken ct)
        {
            Exception ultimo = null;
            foreach (string url in UrlsFfmpeg)
            {
                try
                {
                    string zip = await Descargar(url, "ffmpeg.zip", progreso, ct);
                    string destino = Path.Combine(Carpeta, "ffmpeg");
                    ExtraerArchivos(zip, destino, e => e.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                    File.Delete(zip);
                    string exe = Path.Combine(destino, "ffmpeg.exe");
                    if (File.Exists(exe)) return exe;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { ultimo = ex; }
            }
            throw new IOException("No se pudo descargar FFmpeg. " + (ultimo?.Message ?? ""), ultimo);
        }

        /// <summary>Descarga whisper.cpp (whisper-cli.exe y sus DLL).</summary>
        public static async Task<string> DescargarWhisper(IProgress<(long, long)> progreso, CancellationToken ct)
        {
            string url = UrlWhisperFija;
            try
            {
                using (HttpClient c = Cliente())
                {
                    string json = await c.GetStringAsync(ApiWhisper);
                    Match m = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/whisper-bin-x64\\.zip)\"");
                    if (m.Success) url = m.Groups[1].Value;
                }
            }
            catch { /* si la API falla, se usa el enlace fijo */ }

            string zip = await Descargar(url, "whisper.zip", progreso, ct);
            string destino = Path.Combine(Carpeta, "whisper");
            // Se extraen todos los archivos de la carpeta donde está whisper-cli.exe (el .exe necesita sus DLL).
            string carpetaExe = null;
            using (ZipArchive z = ZipFile.OpenRead(zip))
            {
                ZipArchiveEntry exe = z.Entries.FirstOrDefault(e => e.Name.Equals("whisper-cli.exe", StringComparison.OrdinalIgnoreCase))
                                      ?? z.Entries.FirstOrDefault(e => e.Name.Equals("main.exe", StringComparison.OrdinalIgnoreCase));
                if (exe == null) throw new IOException("El archivo descargado de whisper.cpp no contiene whisper-cli.exe.");
                carpetaExe = exe.FullName.Substring(0, exe.FullName.Length - exe.Name.Length);
            }
            ExtraerArchivos(zip, destino, e => e.StartsWith(carpetaExe, StringComparison.OrdinalIgnoreCase) && !e.EndsWith("/"));
            File.Delete(zip);
            string r = Whisper();
            if (r == null) throw new IOException("No se encontró whisper-cli.exe después de descargarlo.");
            return r;
        }

        /// <summary>Descarga un modelo de Whisper.</summary>
        public static async Task<string> DescargarModelo(ModeloWhisper m, IProgress<(long, long)> progreso, CancellationToken ct)
        {
            string tmp = await Descargar(m.Url, m.Archivo + ".parcial", progreso, ct);
            string destino = Path.Combine(Carpeta, "modelos", m.Archivo);
            Directory.CreateDirectory(Path.GetDirectoryName(destino));
            if (File.Exists(destino)) File.Delete(destino);
            File.Move(tmp, destino);
            return destino;
        }

        private static async Task<string> Descargar(string url, string nombre, IProgress<(long, long)> progreso, CancellationToken ct)
        {
            Directory.CreateDirectory(Carpeta);
            string ruta = Path.Combine(Carpeta, nombre);
            using (HttpClient c = Cliente())
            using (HttpResponseMessage r = await c.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                r.EnsureSuccessStatusCode();
                long total = r.Content.Headers.ContentLength ?? -1;
                using (Stream origen = await r.Content.ReadAsStreamAsync())
                using (var destino = new FileStream(ruta, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true))
                {
                    var buffer = new byte[1 << 16];
                    long leido = 0;
                    int n;
                    while ((n = await origen.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await destino.WriteAsync(buffer, 0, n, ct);
                        leido += n;
                        progreso?.Report((leido, total));
                    }
                }
            }
            return ruta;
        }

        private static void ExtraerArchivos(string zip, string destino, Func<string, bool> filtro)
        {
            Directory.CreateDirectory(destino);
            using (ZipArchive z = ZipFile.OpenRead(zip))
            {
                foreach (ZipArchiveEntry e in z.Entries)
                {
                    if (e.Name == "" || !filtro(e.FullName.Replace('\\', '/'))) continue;
                    e.ExtractToFile(Path.Combine(destino, e.Name), true);
                }
            }
        }
    }
}
