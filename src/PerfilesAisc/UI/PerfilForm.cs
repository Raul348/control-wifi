using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PerfilesAisc.Core;

namespace PerfilesAisc.UI
{
    /// <summary>
    /// Ventana "Seleccionar perfil", con la misma organización que la de Tekla:
    /// nombre y filtro arriba, árbol a la izquierda, y a la derecha las pestañas General (tipo, figura y medidas)
    /// y Análisis (área, peso, inercias y módulos).
    /// </summary>
    internal sealed class PerfilForm : Form
    {
        public enum Accion { Ninguna, Viga, Columna }

        public Accion AccionElegida { get; private set; }
        public Perfil PerfilElegido { get; private set; }

        private static readonly Color Encabezado = Color.FromArgb(31, 56, 100);
        private static readonly Color Gris = Color.FromArgb(110, 118, 129);
        private static readonly Color Fondo = Color.FromArgb(247, 248, 250);

        private readonly Catalogo _catalogo = Catalogo.Instancia;
        private readonly ComboBox _nombre = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 230 };
        private readonly TextBox _filtro = new TextBox { Text = "*", Width = 230 };
        private readonly TreeView _arbol = new TreeView { Dock = DockStyle.Fill, HideSelection = false, ShowLines = true, BorderStyle = BorderStyle.FixedSingle };
        private readonly ComboBox _unidades = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        private readonly Label _tipo = new Label { AutoSize = true };
        private readonly Label _subtipo = new Label { AutoSize = true };
        private readonly Label _area = new Label { AutoSize = true };
        private readonly PictureBox _imagen = new PictureBox { Height = 210, Dock = DockStyle.Top, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        private readonly ListView _general = Tabla();
        private readonly ListView _analisis = Tabla();
        private readonly Label _estado = new Label { AutoSize = true, ForeColor = Gris };
        private Perfil _actual;
        private bool _cambiando;

        public PerfilForm(string nombreInicial)
        {
            Text = "Seleccionar perfil";
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(960, 640);
            MinimumSize = new Size(820, 560);
            Icon = Icon.FromHandle(Iconos.SeccionI(32).GetHicon());

            Controls.Add(CrearCentro());
            Controls.Add(CrearArriba());
            Controls.Add(CrearAbajo());

            _unidades.Items.AddRange(new object[] { "Milímetros", "Pulgadas" });
            _unidades.SelectedIndex = 0;
            _unidades.SelectedIndexChanged += (s, e) => Mostrar(_actual);

            _nombre.Items.AddRange(_catalogo.Perfiles.Select(p => (object)p.Nombre).ToArray());
            _nombre.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _nombre.AutoCompleteSource = AutoCompleteSource.ListItems;
            _nombre.SelectedIndexChanged += (s, e) => { if (!_cambiando) Seleccionar(_nombre.Text); };
            _nombre.Leave += (s, e) => { if (!_cambiando) Seleccionar(_nombre.Text); };
            _nombre.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Seleccionar(_nombre.Text); e.SuppressKeyPress = true; } };

            _filtro.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { LlenarArbol(); e.SuppressKeyPress = true; } };
            _arbol.AfterSelect += (s, e) => { if (e.Node.Tag is Perfil p) Mostrar(p); };
            _arbol.NodeMouseDoubleClick += (s, e) => { if (e.Node.Tag is Perfil) Elegir(Accion.Viga); };
            _imagen.Paint += (s, e) => Seccion.Dibujar(e.Graphics, new RectangleF(0, 0, _imagen.Width, _imagen.Height), _actual);
            _imagen.Resize += (s, e) => _imagen.Invalidate();

            LlenarArbol();
            Seleccionar(nombreInicial ?? "W8X31");
        }

        // ================================================================== construcción

        private Control CrearArriba()
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(12, 8, 12, 0) };
            var f = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            f.Controls.Add(new Label { Text = "Nombre perfil:", AutoSize = true, Margin = new Padding(0, 5, 6, 0) });
            f.Controls.Add(_nombre);
            f.Controls.Add(new Label { Text = "Unidades:", AutoSize = true, Margin = new Padding(24, 5, 6, 0) });
            f.Controls.Add(_unidades);
            p.Controls.Add(f);
            return p;
        }

        private Control CrearCentro()
        {
            var division = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 340, FixedPanel = FixedPanel.Panel1, Panel1MinSize = 260 };

            // Izquierda: filtro y árbol
            var izq = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 6, 6) };
            var filtro = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, WrapContents = false };
            filtro.Controls.Add(new Label { Text = "Filtro:", AutoSize = true, Margin = new Padding(0, 5, 6, 0) });
            filtro.Controls.Add(_filtro);
            filtro.Controls.Add(Boton("Filtro", (s, e) => LlenarArbol()));
            izq.Controls.Add(_arbol);
            izq.Controls.Add(filtro);
            division.Panel1.Controls.Add(izq);

            // Derecha: pestañas
            var pestanas = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };
            pestanas.TabPages.Add(PaginaGeneral());
            pestanas.TabPages.Add(PaginaAnalisis());
            var der = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 4, 12, 6) };
            der.Controls.Add(pestanas);
            division.Panel2.Controls.Add(der);
            return division;
        }

        private TabPage PaginaGeneral()
        {
            var pagina = new TabPage("General") { Padding = new Padding(8), BackColor = Color.White };

            var tipo = new TableLayoutPanel { Dock = DockStyle.Top, Height = 78, ColumnCount = 2, BackColor = Fondo, Padding = new Padding(6, 4, 6, 4) };
            tipo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            tipo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tipo.Controls.Add(new Label { Text = "Tipo perfil:", AutoSize = true, ForeColor = Gris }, 0, 0);
            tipo.Controls.Add(_tipo, 1, 0);
            tipo.Controls.Add(new Label { Text = "Subtipo perfil:", AutoSize = true, ForeColor = Gris }, 0, 1);
            tipo.Controls.Add(_subtipo, 1, 1);
            tipo.Controls.Add(new Label { Text = "Área sección:", AutoSize = true, ForeColor = Gris }, 0, 2);
            tipo.Controls.Add(_area, 1, 2);

            var imagenTitulo = new Label { Text = "Imagen", Dock = DockStyle.Top, Height = 22, ForeColor = Gris, Padding = new Padding(0, 4, 0, 0) };

            pagina.Controls.Add(_general);
            pagina.Controls.Add(new Label { Dock = DockStyle.Top, Height = 6 });
            pagina.Controls.Add(_imagen);
            pagina.Controls.Add(imagenTitulo);
            pagina.Controls.Add(tipo);
            return pagina;
        }

        private TabPage PaginaAnalisis()
        {
            var pagina = new TabPage("Análisis") { Padding = new Padding(8), BackColor = Color.White };
            pagina.Controls.Add(_analisis);
            pagina.Controls.Add(new Label
            {
                Text = "Propiedades de la sección según la base de datos AISC (edición 13).",
                Dock = DockStyle.Top, Height = 24, ForeColor = Gris
            });
            return pagina;
        }

        private Control CrearAbajo()
        {
            var p = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Fondo, Padding = new Padding(12, 10, 12, 10) };
            var botones = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Width = 520 };
            botones.Controls.Add(Boton("Cancelar", (s, e) => { DialogResult = DialogResult.Cancel; Close(); }));
            botones.Controls.Add(Boton("Copiar datos", (s, e) => Copiar()));
            botones.Controls.Add(Boton("Colocar columna", (s, e) => Elegir(Accion.Columna)));
            botones.Controls.Add(Boton("Colocar viga", (s, e) => Elegir(Accion.Viga), principal: true));
            _estado.Location = new Point(12, 16);
            p.Controls.Add(_estado);
            p.Controls.Add(botones);
            return p;
        }

        private static ListView Tabla()
        {
            var t = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable, BorderStyle = BorderStyle.FixedSingle, MultiSelect = false
            };
            t.Columns.Add("Propiedad", 230);
            t.Columns.Add("Símbolo", 70);
            t.Columns.Add("Valor", 110, HorizontalAlignment.Right);
            t.Columns.Add("Unidad", 80);
            return t;
        }

        private static Button Boton(string texto, EventHandler click, bool principal = false)
        {
            var b = new Button
            {
                Text = texto, AutoSize = true, Height = 30, Padding = new Padding(8, 0, 8, 0), Margin = new Padding(8, 0, 0, 0),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                BackColor = principal ? Color.FromArgb(0, 120, 212) : Color.White,
                ForeColor = principal ? Color.White : Color.Black,
            };
            b.FlatAppearance.BorderColor = principal ? Color.FromArgb(0, 120, 212) : Color.FromArgb(200, 204, 210);
            b.Click += click;
            return b;
        }

        // ================================================================== árbol

        private void LlenarArbol()
        {
            List<Nodo> raiz = _catalogo.ArbolFiltrado(_filtro.Text);
            _arbol.BeginUpdate();
            _arbol.Nodes.Clear();
            foreach (Nodo n in raiz) _arbol.Nodes.Add(Convertir(n));
            bool filtrado = _filtro.Text.Trim() != "" && _filtro.Text.Trim() != "*";
            foreach (TreeNode n in _arbol.Nodes)
            {
                n.NodeFont = new Font(Font, FontStyle.Bold);
                if (filtrado) n.ExpandAll(); else n.Expand();
            }
            _arbol.EndUpdate();
            int total = raiz.Sum(n => n.Perfiles().Count());
            _estado.Text = total == _catalogo.Perfiles.Count ? total + " perfiles" : total + " de " + _catalogo.Perfiles.Count + " perfiles";
        }

        private static TreeNode Convertir(Nodo n)
        {
            var t = new TreeNode(n.Texto) { Tag = n.Perfil };
            foreach (Nodo h in n.Hijos) t.Nodes.Add(Convertir(h));
            return t;
        }

        private void Seleccionar(string nombre)
        {
            Perfil p = _catalogo.Buscar(nombre);
            if (p == null) return;
            TreeNode nodo = BuscarNodo(_arbol.Nodes, p);
            if (nodo == null)
            {
                // El filtro lo oculta: se quita el filtro para poder mostrarlo.
                _filtro.Text = "*";
                LlenarArbol();
                nodo = BuscarNodo(_arbol.Nodes, p);
            }
            if (nodo != null)
            {
                _arbol.SelectedNode = nodo;
                nodo.EnsureVisible();
            }
            else Mostrar(p);
        }

        private static TreeNode BuscarNodo(TreeNodeCollection nodos, Perfil p)
        {
            foreach (TreeNode n in nodos)
            {
                if (ReferenceEquals(n.Tag, p)) return n;
                TreeNode h = BuscarNodo(n.Nodes, p);
                if (h != null) return h;
            }
            return null;
        }

        // ================================================================== datos

        private bool EnMm => _unidades.SelectedIndex == 0;

        private void Mostrar(Perfil p)
        {
            _actual = p;
            _imagen.Invalidate();
            if (p == null) return;

            _cambiando = true;
            _nombre.Text = p.Nombre;
            _cambiando = false;

            _tipo.Text = Catalogo.NombreForma(p.Forma) + "   (" + p.Serie + ")";
            _subtipo.Text = Subtipo(p);
            _area.Text = p.AreaIn2 == null ? "–" : EnMm ? Unidades.Texto(Unidades.Cm2(p.AreaIn2.Value)) + " cm²" : Unidades.Texto(p.AreaIn2.Value) + " in²";

            _general.BeginUpdate();
            _general.Items.Clear();
            foreach (var f in FilasGenerales(p)) _general.Items.Add(Fila(f));
            _general.EndUpdate();

            _analisis.BeginUpdate();
            _analisis.Items.Clear();
            foreach (var f in FilasAnalisis(p)) _analisis.Items.Add(Fila(f));
            _analisis.EndUpdate();
        }

        private static ListViewItem Fila((string, string, string, string) f)
        {
            var it = new ListViewItem(f.Item1);
            it.SubItems.Add(f.Item2);
            it.SubItems.Add(f.Item3);
            it.SubItems.Add(f.Item4);
            return it;
        }

        private static string Subtipo(Perfil p)
        {
            switch (p.Forma)
            {
                case Forma.TuboRectangular: return "Conformado en frío (ASTM A500)";
                case Forma.TuboRedondo: return p.Serie == "Pipe" ? "Tubería (ASTM A53)" : "Conformado en frío (ASTM A500)";
                default: return "Laminado en caliente";
            }
        }

        /// <summary>Longitud en la unidad elegida.</summary>
        private (string, string, string, string) L(string nombre, string simbolo, double? pulgadas)
            => (nombre, simbolo, pulgadas == null ? "–" : Unidades.Texto(EnMm ? Unidades.Mm(pulgadas.Value) : pulgadas.Value), EnMm ? "mm" : "in");

        private IEnumerable<(string, string, string, string)> FilasGenerales(Perfil p)
        {
            switch (p.Forma)
            {
                case Forma.I:
                case Forma.T:
                    yield return L("Altura", "h", p.V("d"));
                    yield return L("Ancho", "b", p.V("bf"));
                    yield return L("Espesor de alma", "s", p.V("tw"));
                    yield return L("Espesor de ala", "t", p.V("tf"));
                    yield return L("Radio de redondeo 1", "r1", p.RadioIn);
                    yield return L("Distancia k (ala a fin del acuerdo)", "k", p.V("k"));
                    if (p.Tiene("k1")) yield return L("Distancia k1", "k1", p.V("k1"));
                    if (p.Tiene("T")) yield return L("Altura libre del alma", "T", p.V("T"));
                    if (p.Tiene("ybar")) yield return L("Centro de gravedad", "ȳ", p.V("ybar"));
                    break;
                case Forma.Canal:
                    yield return L("Altura", "h", p.V("d"));
                    yield return L("Ancho", "b", p.V("bf"));
                    yield return L("Espesor de alma", "s", p.V("tw"));
                    yield return L("Espesor de ala", "t", p.V("tf"));
                    yield return L("Radio de redondeo 1", "r1", p.RadioIn);
                    yield return L("Distancia k", "k", p.V("k"));
                    yield return L("Centro de gravedad", "x̄", p.V("xbar"));
                    yield return L("Centro de cortante", "eo", p.V("eo"));
                    break;
                case Forma.Angulo:
                    yield return L("Lado largo", "a", p.V("d"));
                    yield return L("Lado corto", "b", p.V("b"));
                    yield return L("Espesor", "t", p.V("t"));
                    yield return L("Radio de redondeo 1", "r1", p.RadioIn);
                    yield return L("Centro de gravedad", "x̄", p.V("xbar"));
                    yield return L("Centro de gravedad", "ȳ", p.V("ybar"));
                    break;
                case Forma.AnguloDoble:
                    yield return L("Lado largo (cada ángulo)", "a", p.V("d"));
                    yield return L("Lado corto (cada ángulo)", "b", p.V("b"));
                    yield return L("Espesor", "t", p.V("t"));
                    yield return L("Separación entre ángulos", "e", 0.375);
                    yield return L("Centro de gravedad", "ȳ", p.V("ybar"));
                    break;
                case Forma.TuboRectangular:
                    yield return L("Altura", "h", p.V("h"));
                    yield return L("Ancho", "b", p.V("b"));
                    yield return L("Espesor nominal", "t", p.V("tnom"));
                    yield return L("Espesor de diseño", "td", p.V("tdes"));
                    yield return L("Radio exterior (2 t)", "r", 2 * (p.V("tdes") ?? 0));
                    break;
                default:
                    yield return L("Diámetro exterior", "D", p.V("OD"));
                    yield return L("Diámetro interior", "Di", p.V("ID"));
                    yield return L("Espesor nominal", "t", p.V("tnom"));
                    yield return L("Espesor de diseño", "td", p.V("tdes"));
                    break;
            }
        }

        private IEnumerable<(string, string, string, string)> FilasAnalisis(Perfil p)
        {
            bool mm = EnMm;
            (string, string, string, string) F(string nombre, string simbolo, string col, Func<double, double> conv, string uMetrica, string uImperial)
            {
                double? v = p.V(col);
                return (nombre, simbolo, v == null ? "–" : Unidades.Texto(mm ? conv(v.Value) : v.Value), mm ? uMetrica : uImperial);
            }
            yield return F("Área", "A", "A", Unidades.Cm2, "cm²", "in²");
            yield return F("Peso por metro", "w", "wt", Unidades.KgM, "kg/m", "lb/ft");
            yield return F("Momento de inercia eje fuerte", "Ix", "Ix", Unidades.Cm4, "cm⁴", "in⁴");
            yield return F("Módulo elástico eje fuerte", "Sx", "Sx", Unidades.Cm3, "cm³", "in³");
            yield return F("Radio de giro eje fuerte", "rx", "rx", Unidades.Cm, "cm", "in");
            yield return F("Módulo plástico eje fuerte", "Zx", "Zx", Unidades.Cm3, "cm³", "in³");
            yield return F("Momento de inercia eje débil", "Iy", "Iy", Unidades.Cm4, "cm⁴", "in⁴");
            yield return F("Módulo elástico eje débil", "Sy", "Sy", Unidades.Cm3, "cm³", "in³");
            yield return F("Radio de giro eje débil", "ry", "ry", Unidades.Cm, "cm", "in");
            yield return F("Módulo plástico eje débil", "Zy", "Zy", Unidades.Cm3, "cm³", "in³");
            if (p.Tiene("rz")) yield return F("Radio de giro mínimo", "rz", "rz", Unidades.Cm, "cm", "in");
            yield return F("Constante torsional", "J", "J", Unidades.Cm4, "cm⁴", "in⁴");
            if (p.Tiene("Cw")) yield return F("Constante de alabeo", "Cw", "Cw", Unidades.Cm6, "cm⁶", "in⁶");
            if (p.Tiene("Asurf")) yield return F("Área de superficie (pintura)", "As", "Asurf", Unidades.M2M, "m²/m", "ft²/ft");
        }

        // ================================================================== acciones

        private void Elegir(Accion accion)
        {
            if (_actual == null)
            {
                MessageBox.Show(this, "Elija un perfil en el árbol.", App.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AccionElegida = accion;
            PerfilElegido = _actual;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Copiar()
        {
            if (_actual == null) return;
            var sb = new StringBuilder();
            sb.AppendLine(_actual.Nombre + "  (" + _tipo.Text + ", " + _subtipo.Text + ")");
            foreach (ListViewItem it in _general.Items) sb.AppendLine(it.Text + "\t" + it.SubItems[1].Text + "\t" + it.SubItems[2].Text + "\t" + it.SubItems[3].Text);
            foreach (ListViewItem it in _analisis.Items) sb.AppendLine(it.Text + "\t" + it.SubItems[1].Text + "\t" + it.SubItems[2].Text + "\t" + it.SubItems[3].Text);
            try { Clipboard.SetText(sb.ToString()); _estado.Text = "Datos de " + _actual.Nombre + " copiados al portapapeles."; }
            catch { _estado.Text = "No se pudo copiar."; }
        }
    }
}
