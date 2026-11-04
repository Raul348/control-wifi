using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using PerfilesAisc.Core;

namespace PerfilesAisc.UI
{
    /// <summary>Ventana "Nave curva": medidas de la nave, perfiles de cada grupo y figura del pórtico.</summary>
    internal sealed class NaveForm : Form
    {
        private static readonly Color Gris = Color.FromArgb(110, 118, 129);
        private static readonly Color Fondo = Color.FromArgb(247, 248, 250);

        private readonly Catalogo _catalogo = Catalogo.Instancia;
        private readonly Dictionary<string, TextBox> _campos = new Dictionary<string, TextBox>();
        private readonly ComboBox _tipo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private readonly CheckBox _amarre = new CheckBox { Text = "Vigas de amarre entre columnas", AutoSize = true };
        private readonly CheckBox _arriostres = new CheckBox { Text = "Arriostres en cruz en los vanos extremos", AutoSize = true };
        private readonly CheckBox _otroLado = new CheckBox { Text = "Generar al otro lado de la dirección elegida", AutoSize = true };
        private readonly Dictionary<GrupoBarra, ComboBox> _perfiles = new Dictionary<GrupoBarra, ComboBox>();
        private readonly PictureBox _figura = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        private readonly Label _resumen = new Label { Dock = DockStyle.Bottom, Height = 60, ForeColor = Gris, Padding = new Padding(4) };

        public ParametrosNave Parametros { get; private set; }
        public Dictionary<GrupoBarra, Perfil> Perfiles { get; } = new Dictionary<GrupoBarra, Perfil>();
        public bool OtroLado => _otroLado.Checked;

        public NaveForm()
        {
            Text = "Nave industrial con cubierta en arco";
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 620);
            MinimumSize = new Size(900, 560);
            Icon = Icon.FromHandle(Iconos.SeccionI(32).GetHicon());

            var division = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 430, FixedPanel = FixedPanel.Panel1, Panel1MinSize = 400 };
            division.Panel1.Controls.Add(CrearDatos());
            var der = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 10, 12, 6) };
            der.Controls.Add(_figura);
            der.Controls.Add(_resumen);
            der.Controls.Add(new Label { Text = "Pórtico tipo (elevación)", Dock = DockStyle.Top, Height = 22, ForeColor = Gris });
            division.Panel2.Controls.Add(der);
            Controls.Add(division);
            Controls.Add(CrearAbajo());

            _figura.Paint += (s, e) => Dibujar(e.Graphics, _figura.ClientRectangle);
            _figura.Resize += (s, e) => _figura.Invalidate();
            Cargar();
            Actualizar();
        }

        // ================================================================== construcción

        private Control CrearDatos()
        {
            var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoScroll = true, Padding = new Padding(12, 10, 6, 6) };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Titulo(tabla, "Medidas (metros)");
            Campo(tabla, "luz", "Luz entre ejes de columnas", "20");
            Campo(tabla, "altura", "Altura de columnas", "6");
            Campo(tabla, "flecha", "Flecha del arco (sobre el apoyo)", "3");
            Campo(tabla, "porticos", "Número de pórticos", "5");
            Campo(tabla, "separacion", "Separación entre pórticos", "6");

            Titulo(tabla, "Arco");
            _tipo.Items.AddRange(new object[] { "Viga curva", "Tramos rectos", "Celosía en arco" });
            _tipo.SelectedIndex = 0;
            _tipo.SelectedIndexChanged += (s, e) => Actualizar();
            Fila(tabla, "Tipo de arco", _tipo);
            Campo(tabla, "tramos", "Tramos rectos / paneles de celosía", "8");
            Campo(tabla, "canto", "Canto de la celosía (m)", "1");
            Campo(tabla, "correas", "Correas por arco (sin los extremos)", "6");
            Fila(tabla, "", _amarre);
            Fila(tabla, "", _arriostres);
            Fila(tabla, "", _otroLado);
            _amarre.CheckedChanged += (s, e) => Actualizar();
            _arriostres.CheckedChanged += (s, e) => Actualizar();

            Titulo(tabla, "Perfiles");
            SelectorPerfil(tabla, GrupoBarra.Columna, "Columnas", "W10X49");
            SelectorPerfil(tabla, GrupoBarra.Arco, "Arco / cordones de la celosía", "W8X31");
            SelectorPerfil(tabla, GrupoBarra.Correa, "Correas y vigas de amarre", "C6X10.5");
            SelectorPerfil(tabla, GrupoBarra.Diagonal, "Montantes, diagonales y arriostres", "L3X3X1/4");
            return tabla;
        }

        private static void Titulo(TableLayoutPanel t, string texto)
        {
            var l = new Label { Text = texto, AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5f), Margin = new Padding(0, 10, 0, 4) };
            t.Controls.Add(l, 0, t.RowCount);
            t.SetColumnSpan(l, 3);
            t.RowCount++;
        }

        private void Campo(TableLayoutPanel t, string clave, string texto, string valor)
        {
            var caja = new TextBox { Width = 100, Text = valor };
            caja.TextChanged += (s, e) => Actualizar();
            _campos[clave] = caja;
            Fila(t, texto, caja);
        }

        private static void Fila(TableLayoutPanel t, string texto, Control c)
        {
            int fila = t.RowCount;
            if (texto != "")
            {
                t.Controls.Add(new Label { Text = texto, AutoSize = true, Margin = new Padding(0, 6, 0, 0) }, 0, fila);
                t.Controls.Add(c, 1, fila);
            }
            else
            {
                t.Controls.Add(c, 0, fila);
                t.SetColumnSpan(c, 3);
            }
            t.RowCount++;
        }

        private void SelectorPerfil(TableLayoutPanel t, GrupoBarra grupo, string texto, string porDefecto)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 120, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
            combo.Items.AddRange(_catalogo.Perfiles.Select(p => (object)p.Nombre).ToArray());
            combo.Text = porDefecto;
            _perfiles[grupo] = combo;
            var boton = new Button { Text = "…", Width = 32, Height = 24, Margin = new Padding(4, 2, 0, 0) };
            boton.Click += (s, e) =>
            {
                using (var f = new PerfilForm(combo.Text, soloElegir: true))
                    if (f.ShowDialog(this) == DialogResult.OK && f.PerfilElegido != null) combo.Text = f.PerfilElegido.Nombre;
            };
            int fila = t.RowCount;
            t.Controls.Add(new Label { Text = texto, AutoSize = true, Margin = new Padding(0, 6, 0, 0) }, 0, fila);
            t.Controls.Add(combo, 1, fila);
            t.Controls.Add(boton, 2, fila);
            t.RowCount++;
        }

        private Control CrearAbajo()
        {
            var p = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Fondo, Padding = new Padding(12, 10, 12, 10) };
            var botones = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Width = 300 };
            var cancelar = new Button { Text = "Cancelar", AutoSize = true, Height = 30, Margin = new Padding(8, 0, 0, 0), DialogResult = DialogResult.Cancel };
            var generar = new Button
            {
                Text = "Generar nave", AutoSize = true, Height = 30, Margin = new Padding(8, 0, 0, 0), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, Cursor = Cursors.Hand
            };
            generar.Click += (s, e) => Aceptar();
            botones.Controls.Add(cancelar);
            botones.Controls.Add(generar);
            p.Controls.Add(new Label
            {
                Text = "Al pulsar Generar, elija en el modelo el punto de la primera columna y luego la dirección a lo largo de la nave.",
                AutoSize = true, ForeColor = Gris, Location = new Point(12, 16), MaximumSize = new Size(640, 0)
            });
            p.Controls.Add(botones);
            AcceptButton = generar;
            CancelButton = cancelar;
            return p;
        }

        // ================================================================== datos

        private double Num(string clave, double porDefecto)
        {
            string s = _campos[clave].Text.Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out double v)) return v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return porDefecto;
        }

        private ParametrosNave Leer() => new ParametrosNave
        {
            Luz = Num("luz", 0), Altura = Num("altura", 0), Flecha = Num("flecha", 0),
            Porticos = (int)Num("porticos", 0), Separacion = Num("separacion", 0),
            Tipo = (TipoArco)_tipo.SelectedIndex, Tramos = (int)Num("tramos", 0), Canto = Num("canto", 0),
            Correas = (int)Num("correas", 0), VigaAmarre = _amarre.Checked, Arriostres = _arriostres.Checked,
        };

        private void Actualizar()
        {
            _campos["canto"].Enabled = _tipo.SelectedIndex == 2;
            _campos["tramos"].Enabled = _tipo.SelectedIndex != 0;
            ParametrosNave p = Leer();
            string error = p.Validar();
            if (error != null) _resumen.Text = error;
            else
            {
                var r = Nave.Resumen(Nave.Generar(p));
                _resumen.Text = string.Join("   ·   ", r.Select(x => Nave.NombreGrupo(x.Grupo) + ": " + x.Cantidad + " (" + x.Longitud.ToString("N0") + " m)"));
            }
            _figura.Invalidate();
        }

        private void Aceptar()
        {
            ParametrosNave p = Leer();
            string error = p.Validar();
            if (error != null)
            {
                MessageBox.Show(this, error, App.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Perfiles.Clear();
            foreach (var kv in _perfiles)
            {
                Perfil perfil = _catalogo.Buscar(kv.Value.Text);
                if (perfil == null)
                {
                    MessageBox.Show(this, "El perfil \"" + kv.Value.Text + "\" no está en el catálogo. Elíjalo con el botón …", App.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Perfiles[kv.Key] = perfil;
            }
            Parametros = p;
            Guardar();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cargar()
        {
            foreach (var kv in _campos) kv.Value.Text = Ajustes.Leer("nave." + kv.Key, kv.Value.Text);
            _tipo.SelectedIndex = Math.Max(0, Math.Min(2, Ajustes.Leer("nave.tipo", 0)));
            _amarre.Checked = Ajustes.Leer("nave.amarre", true);
            _arriostres.Checked = Ajustes.Leer("nave.arriostres", true);
            foreach (var kv in _perfiles) kv.Value.Text = Ajustes.Leer("nave.perfil." + kv.Key, kv.Value.Text);
        }

        private void Guardar()
        {
            foreach (var kv in _campos) Ajustes.Guardar("nave." + kv.Key, kv.Value.Text);
            Ajustes.Guardar("nave.tipo", _tipo.SelectedIndex);
            Ajustes.Guardar("nave.amarre", _amarre.Checked);
            Ajustes.Guardar("nave.arriostres", _arriostres.Checked);
            foreach (var kv in _perfiles) Ajustes.Guardar("nave.perfil." + kv.Key, kv.Value.Text);
        }

        // ================================================================== figura

        private void Dibujar(Graphics g, Rectangle area)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            ParametrosNave p = Leer();
            if (p.Validar() != null) return;

            List<Barra> barras = Nave.Generar(new ParametrosNave
            {
                Luz = p.Luz, Altura = p.Altura, Flecha = p.Flecha, Porticos = 2, Separacion = p.Separacion, Tipo = p.Tipo,
                Tramos = p.Tramos, Canto = p.Canto, Correas = p.Correas, VigaAmarre = false, Arriostres = false
            }).Where(b => Math.Abs(b.A.Y) < 1e-9 && Math.Abs(b.B.Y) < 1e-9).ToList();

            double alto = p.Altura + p.Flecha;
            float margen = 40;
            float esc = (float)Math.Min((area.Width - 2 * margen) / p.Luz, (area.Height - 2 * margen) / alto);
            float ox = area.Left + (area.Width - (float)(p.Luz * esc)) / 2;
            float oy = area.Bottom - margen;
            PointF P(P3 q) => new PointF(ox + (float)(q.X * esc), oy - (float)(q.Z * esc));

            using (var suelo = new Pen(Color.FromArgb(160, 160, 160), 1f) { DashStyle = DashStyle.Dash })
                g.DrawLine(suelo, area.Left + 10, oy, area.Right - 10, oy);

            using (var grueso = new Pen(Color.FromArgb(40, 50, 70), 3f))
            using (var fino = new Pen(Color.FromArgb(90, 110, 140), 1.5f))
            using (var correa = new SolidBrush(Color.FromArgb(0, 120, 212)))
            {
                foreach (Barra b in barras)
                {
                    Pen lapiz = b.Grupo == GrupoBarra.Columna || b.Grupo == GrupoBarra.Arco || b.Grupo == GrupoBarra.CordonSuperior || b.Grupo == GrupoBarra.CordonInferior ? grueso : fino;
                    if (b.Curva)
                    {
                        // Arco por tres puntos: se dibuja con segmentos
                        var pts = new List<PointF>();
                        for (int k = 0; k <= 40; k++) pts.Add(P(Nave.PuntoArco(p.Luz, p.Altura, p.Flecha, k / 40.0)));
                        g.DrawLines(lapiz, pts.ToArray());
                    }
                    else g.DrawLine(lapiz, P(b.A), P(b.B));
                }
                // Correas como puntos sobre el arco
                for (int k = 0; k <= p.Correas + 1; k++)
                {
                    PointF c = P(Nave.PuntoArco(p.Luz, p.Altura, p.Flecha, (double)k / (p.Correas + 1)));
                    g.FillEllipse(correa, c.X - 3, c.Y - 3, 6, 6);
                }
            }

            using (var cota = new Pen(Color.FromArgb(0, 90, 180), 1f) { StartCap = LineCap.ArrowAnchor, EndCap = LineCap.ArrowAnchor })
            using (var texto = new SolidBrush(Color.FromArgb(0, 90, 180)))
            using (var letra = new Font("Segoe UI", 8.5f))
            {
                PointF a = P(new P3(0, 0, 0)), b = P(new P3(p.Luz, 0, 0)), c = P(new P3(0, 0, p.Altura)), d = P(new P3(p.Luz / 2, 0, p.Altura + p.Flecha));
                g.DrawLine(cota, a.X, a.Y + 18, b.X, b.Y + 18);
                g.DrawString("L = " + p.Luz.ToString("0.##") + " m", letra, texto, (a.X + b.X) / 2 - 30, a.Y + 20);
                g.DrawLine(cota, a.X - 18, a.Y, c.X - 18, c.Y);
                g.DrawString("H = " + p.Altura.ToString("0.##"), letra, texto, a.X - 70, (a.Y + c.Y) / 2 - 8);
                g.DrawLine(cota, b.X + 18, c.Y, b.X + 18, d.Y);
                g.DrawString("f = " + p.Flecha.ToString("0.##"), letra, texto, b.X + 22, (c.Y + d.Y) / 2 - 8);
                g.DrawString(p.Porticos + " pórticos cada " + p.Separacion.ToString("0.##") + " m  ·  largo " + ((p.Porticos - 1) * p.Separacion).ToString("0.##") + " m",
                    letra, texto, area.Left + 10, area.Top + 8);
            }
        }
    }
}
