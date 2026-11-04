using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using PerfilesAisc.Core;

namespace PerfilesAisc.UI
{
    /// <summary>
    /// Dibuja la sección del perfil a escala, con las letras de las medidas como en Tekla:
    /// h (altura), b (ancho), s (alma), t (ala), r1 (radio).
    /// </summary>
    internal static class Seccion
    {
        private static readonly Color Relleno = Color.FromArgb(200, 208, 220);
        private static readonly Color Borde = Color.FromArgb(40, 50, 70);
        private static readonly Color Cota = Color.FromArgb(0, 90, 180);
        private static readonly Font Letra = new Font("Segoe UI", 8.5f, FontStyle.Italic);

        public static void Dibujar(Graphics g, RectangleF area, Perfil p)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            if (p == null) return;

            double h = p.AltoIn ?? 1, b = p.AnchoIn ?? 1;
            if (p.Forma == Forma.AnguloDoble) b = 2 * b + 0.375;
            // Escala: la figura ocupa el área dejando margen para las letras.
            RectangleF r = new RectangleF(area.Left + 34, area.Top + 14, area.Width - 60, area.Height - 44);
            float esc = (float)Math.Min(r.Width / b, r.Height / h);
            float ox = r.Left + (r.Width - (float)(b * esc)) / 2;
            float oy = r.Top + (r.Height - (float)(h * esc)) / 2;
            float H = (float)(h * esc), B = (float)(b * esc);

            using (var ruta = Contorno(p, ox, oy, B, H, esc))
            using (var relleno = new SolidBrush(Relleno))
            using (var borde = new Pen(Borde, 1.3f))
            {
                g.FillPath(relleno, ruta);
                g.DrawPath(borde, ruta);
            }
            Cotas(g, p, ox, oy, B, H, esc);
        }

        private static float Mm(double? pulgadas, float esc, float minimo = 1.5f)
            => Math.Max(minimo, (float)((pulgadas ?? 0) * esc));

        private static GraphicsPath Contorno(Perfil p, float x, float y, float B, float H, float esc)
        {
            var ruta = new GraphicsPath(FillMode.Alternate);
            float s = Mm(p.AlmaIn, esc), t = Mm(p.AlaIn, esc);
            float cx = x + B / 2;
            switch (p.Forma)
            {
                case Forma.I:
                    ruta.AddPolygon(new[]
                    {
                        P(x, y), P(x + B, y), P(x + B, y + t), P(cx + s / 2, y + t), P(cx + s / 2, y + H - t),
                        P(x + B, y + H - t), P(x + B, y + H), P(x, y + H), P(x, y + H - t), P(cx - s / 2, y + H - t),
                        P(cx - s / 2, y + t), P(x, y + t),
                    });
                    break;
                case Forma.Canal:
                    ruta.AddPolygon(new[]
                    {
                        P(x, y), P(x + B, y), P(x + B, y + t), P(x + s, y + t), P(x + s, y + H - t),
                        P(x + B, y + H - t), P(x + B, y + H), P(x, y + H),
                    });
                    break;
                case Forma.T:
                    ruta.AddPolygon(new[]
                    {
                        P(x, y), P(x + B, y), P(x + B, y + t), P(cx + s / 2, y + t), P(cx + s / 2, y + H),
                        P(cx - s / 2, y + H), P(cx - s / 2, y + t), P(x, y + t),
                    });
                    break;
                case Forma.Angulo:
                    ruta.AddPolygon(new[]
                    {
                        P(x, y), P(x + t, y), P(x + t, y + H - t), P(x + B, y + H - t), P(x + B, y + H), P(x, y + H),
                    });
                    break;
                case Forma.AnguloDoble:
                {
                    float sep = (float)(0.375 * esc), ala = (B - sep) / 2;
                    ruta.AddPolygon(new[]
                    {
                        P(x + ala - t, y), P(x + ala, y), P(x + ala, y + H), P(x, y + H), P(x, y + H - t), P(x + ala - t, y + H - t),
                    });
                    float x2 = x + ala + sep;
                    ruta.AddPolygon(new[]
                    {
                        P(x2, y), P(x2 + t, y), P(x2 + t, y + H - t), P(x2 + ala, y + H - t), P(x2 + ala, y + H), P(x2, y + H),
                    });
                    break;
                }
                case Forma.TuboRectangular:
                {
                    float re = Math.Min(2 * t, Math.Min(B, H) / 4), ri = Math.Max(0.5f, re - t);
                    AgregarRedondeado(ruta, x, y, B, H, re);
                    AgregarRedondeado(ruta, x + t, y + t, B - 2 * t, H - 2 * t, ri);
                    break;
                }
                default:
                    ruta.AddEllipse(x, y, B, H);
                    ruta.AddEllipse(x + t, y + t, B - 2 * t, H - 2 * t);
                    break;
            }
            return ruta;
        }

        private static PointF P(float x, float y) => new PointF(x, y);

        private static void AgregarRedondeado(GraphicsPath ruta, float x, float y, float w, float h, float r)
        {
            r = Math.Max(0.1f, Math.Min(r, Math.Min(w, h) / 2));
            float d = 2 * r;
            ruta.StartFigure();
            ruta.AddArc(x, y, d, d, 180, 90);
            ruta.AddArc(x + w - d, y, d, d, 270, 90);
            ruta.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            ruta.AddArc(x, y + h - d, d, d, 90, 90);
            ruta.CloseFigure();
        }

        /// <summary>Letras de las medidas, con una línea de cota para h y b.</summary>
        private static void Cotas(Graphics g, Perfil p, float x, float y, float B, float H, float esc)
        {
            using (var lapiz = new Pen(Cota, 1f) { StartCap = LineCap.ArrowAnchor, EndCap = LineCap.ArrowAnchor })
            using (var pincel = new SolidBrush(Cota))
            {
                bool redondo = p.Forma == Forma.TuboRedondo;
                // h a la izquierda
                g.DrawLine(lapiz, x - 14, y, x - 14, y + H);
                g.DrawString(redondo ? "D" : "h", Letra, pincel, x - 30, y + H / 2 - 8);
                // b abajo
                if (!redondo)
                {
                    g.DrawLine(lapiz, x, y + H + 12, x + B, y + H + 12);
                    g.DrawString("b", Letra, pincel, x + B / 2 - 4, y + H + 14);
                }
                float s = Mm(p.AlmaIn, esc), t = Mm(p.AlaIn, esc);
                switch (p.Forma)
                {
                    case Forma.I:
                    case Forma.T:
                        g.DrawString("t", Letra, pincel, x + B + 3, y + t / 2 - 8);
                        g.DrawString("s", Letra, pincel, x + B / 2 + s / 2 + 2, y + H / 2 - 8);
                        if (p.RadioIn != null) g.DrawString("r1", Letra, pincel, x + B / 2 - s / 2 - 18, y + t + 1);
                        break;
                    case Forma.Canal:
                        g.DrawString("t", Letra, pincel, x + B + 3, y + t / 2 - 8);
                        g.DrawString("s", Letra, pincel, x + s + 2, y + H / 2 - 8);
                        break;
                    case Forma.Angulo:
                    case Forma.AnguloDoble:
                        g.DrawString("t", Letra, pincel, x + B + 3, y + H - t - 8);
                        break;
                    default:
                        g.DrawString("t", Letra, pincel, x + B + 3, y + t / 2 - 8);
                        break;
                }
            }
        }
    }
}
