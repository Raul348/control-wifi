using System;
using System.Windows.Forms;

namespace TeklaPiezas
{
    internal static class Program
    {
        public const string Nombre = "Tekla Piezas";

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
                MessageBox.Show("Error inesperado:\n" + e.Exception.Message, Nombre, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Run(new MainForm());
        }
    }
}
