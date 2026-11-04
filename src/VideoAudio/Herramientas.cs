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

        // whisper.cpp: las versiones v* recientes ya no traen el programa para Windows; solo lo traen las versiones
        // de desarrollo (b*) y las v1.7/v1.8 antiguas. Por eso se recorren todas las versiones, de la más nueva a la más
        // antigua, y se usa la primera que tenga whisper-bin-x64.zip.
        private const string ApiWhisper = "https://api.github.com/repos/ggml-org/whisper.cpp/releases?per_page=100";
        private static readonly string[] UrlsWhisperFijas =
        {
            "https://github.com/ggml-org/whisper.cpp/releases/download/v1.7.6/whisper-bin-x64.zip",
            "https://github.com/ggml-org/whisper.cpp/releases/download/v1.7.5/whisper-bin-x64.zip",
            "https://github.com/ggml-org/whisper.cpp/releases/download/v1.7.4/whisper-bin-x64.zip",
        };
        public const string PaginaWhisper = "https://github.com/ggml-org/whisper.cpp/releases";

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
            throw new IOException("No se pudo descargar FFmpeg (" + (ultimo?.Message ?? "") + ").", ultimo);
        }

        /// <summary>Descarga whisper.cpp (whisper-cli.exe y sus DLL).</summary>
        public static async Task<string> DescargarWhisper(IProgress<(long, long)> progreso, CancellationToken ct)
        {
            var urls = new System.Collections.Generic.List<string>();
            try
            {
                using (HttpClient c = Cliente())
                {
                    string json = await c.GetStringAsync(ApiWhisper);
                    // La lista viene de la versión más nueva a la más antigua.
                    foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/whisper-bin-x64\\.zip)\""))
                        if (!urls.Contains(m.Groups[1].Value)) urls.Add(m.Groups[1].Value);
                }
            }
            catch { /* si la API falla, se usan los enlaces fijos */ }
            urls.AddRange(UrlsWhisperFijas);

            string zip = null;
            Exception ultimo = null;
            foreach (string url in urls)
            {
                try
                {
                    zip = await Descargar(url, "whisper.zip", progreso, ct);
                    break;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { ultimo = ex; }
            }
            if (zip == null)
                throw new IOException("No se pudo descargar whisper.cpp (" + (ultimo?.Message ?? "sin enlaces") + ").", ultimo);

            ExtraerWhisper(zip, Path.Combine(Carpeta, "whisper"));
            File.Delete(zip);
            string r = Whisper();
            if (r == null) throw new IOException("No se encontró whisper-cli.exe después de descargarlo.");
            return r;
        }

        /// <summary>
        /// Extrae whisper-cli.exe y todos los archivos de su misma carpeta del ZIP (el .exe necesita sus DLL).
        /// Devuelve la ruta de whisper-cli.exe extraído.
        /// </summary>
        internal static string ExtraerWhisper(string zip, string destino)
        {
            string carpetaExe, nombreExe;
            using (ZipArchive z = ZipFile.OpenRead(zip))
            {
                ZipArchiveEntry exe = z.Entries.FirstOrDefault(e => Normal(e.FullName).EndsWith("/whisper-cli.exe", StringComparison.OrdinalIgnoreCase) || Normal(e.FullName).Equals("whisper-cli.exe", StringComparison.OrdinalIgnoreCase))
                                      ?? z.Entries.FirstOrDefault(e => Normal(e.FullName).EndsWith("/main.exe", StringComparison.OrdinalIgnoreCase));
                if (exe == null) throw new IOException("El archivo descargado de whisper.cpp no contiene whisper-cli.exe.");
                // PowerShell 5 guarda las rutas del ZIP con "\": se normalizan a "/" para comparar.
                string ruta = Normal(exe.FullName);
                carpetaExe = ruta.Substring(0, ruta.LastIndexOf('/') + 1);
                nombreExe = ruta.Substring(carpetaExe.Length);
            }
            ExtraerArchivos(zip, destino, e => e.StartsWith(carpetaExe, StringComparison.OrdinalIgnoreCase) && !e.EndsWith("/"));
            return Path.Combine(destino, nombreExe);
        }

        private static string Normal(string ruta) => ruta.Replace('\\', '/');

        /// <summary>Descarga un modelo de Whisper.</summary>
        public static async Task<string> DescargarModelo(ModeloWhisper m, IProgress<(long, long)> progreso, CancellationToken ct)
        {
            string tmp;
            try { tmp = await Descargar(m.Url, m.Archivo + ".parcial", progreso, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new IOException("No se pudo descargar el modelo " + m.Archivo + " (" + ex.Message + ").", ex); }
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
                    string ruta = Normal(e.FullName);
                    string nombre = ruta.Substring(ruta.LastIndexOf('/') + 1);
                    if (nombre == "" || !filtro(ruta)) continue;
                    e.ExtractToFile(Path.Combine(destino, nombre), true);
                }
            }
        }
    }
}
