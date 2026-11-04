using System;
using System.Net;
using System.Windows.Forms;

namespace VideoAudio
{
    internal static class Program
    {
        public const string Nombre = "Video a Audio";

        [STAThread]
        private static void Main(string[] args)
        {
            // .NET Framework 4.8 en algunos equipos no activa TLS 1.2 por defecto; las descargas lo necesitan.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
                MessageBox.Show("Error inesperado:\n" + e.Exception.Message, Nombre, MessageBoxButtons.OK, MessageBoxIcon.Error);
            // Se pueden pasar videos al programa: arrastrarlos sobre el .exe o "Enviar a".
            Application.Run(new MainForm(args));
        }
    }
}
