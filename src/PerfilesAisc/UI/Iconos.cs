using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using ImageSource = System.Windows.Media.ImageSource;

namespace PerfilesAisc.UI
{
    /// <summary>Ícono del botón de la cinta (una sección I dibujada) y su conversión a imagen WPF.</summary>
    internal static class Iconos
    {
        public static readonly Color Acero = Color.FromArgb(70, 90, 120);

        public static ImageSource Cinta(int tam)
        {
            using (Bitmap bmp = SeccionI(tam))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
                return img;
            }
        }

        public static ImageSource CintaNave(int tam) => AImagen(Nave(tam));

        private static ImageSource AImagen(Bitmap bmp)
        {
            using (bmp)
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
                return img;
            }
        }

        /// <summary>Pórtico con arco: dos columnas y un arco encima.</summary>
        public static Bitmap Nave(int tam)
        {
            var bmp = new Bitmap(tam, tam);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float m = tam * 0.1f, w = tam - 2 * m, h = tam - 2 * m;
                float grosor = tam >= 32 ? 3f : 2f;
                using (var p = new Pen(Acero, grosor) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    float yBase = m + h, yCol = m + h * 0.55f;
                    g.DrawLine(p, m, yBase, m, yCol);
                    g.DrawLine(p, m + w, yBase, m + w, yCol);
                    // arco: semicírculo achatado
                    g.DrawArc(p, m, m, w, 2 * (yCol - m), 180, 180);
                }
            }
            return bmp;
        }

        public static Bitmap SeccionI(int tam)
        {
            var bmp = new Bitmap(tam, tam);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float m = tam * 0.12f, w = tam - 2 * m, h = tam - 2 * m;
                float t = w * 0.2f;
                var ruta = new GraphicsPath();
                ruta.AddPolygon(new[]
                {
                    new PointF(m, m), new PointF(m + w, m), new PointF(m + w, m + t),
                    new PointF(m + w / 2 + t / 2, m + t), new PointF(m + w / 2 + t / 2, m + h - t),
                    new PointF(m + w, m + h - t), new PointF(m + w, m + h), new PointF(m, m + h),
                    new PointF(m, m + h - t), new PointF(m + w / 2 - t / 2, m + h - t),
                    new PointF(m + w / 2 - t / 2, m + t), new PointF(m, m + t),
                });
                using (var b = new SolidBrush(Acero)) g.FillPath(b, ruta);
                using (var p = new Pen(Color.FromArgb(30, 40, 60), tam >= 32 ? 1.5f : 1f)) g.DrawPath(p, ruta);
            }
            return bmp;
        }
    }
}
