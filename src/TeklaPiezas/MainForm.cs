using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeklaPiezas.Core;

namespace TeklaPiezas
{
    /// <summary>
    /// Ventana principal. Lee las piezas del modelo abierto en Tekla y las muestra en cuatro pestañas:
    /// lista de piezas, resumen por perfil (metrado), revisión antes de numerar y catálogo.
    /// </summary>
    internal sealed class MainForm : Form
    {
        // Colores
        private static readonly Color Azul = Color.FromArgb(31, 56, 100);
        private static readonly Color AzulClaro = Color.FromArgb(205, 215, 230);
        private static readonly Color Gris = Color.FromArgb(247, 248, 250);
        private static readonly Color Borde = Color.FromArgb(220, 223, 228);

        private readonly TeklaLector _tekla = new TeklaLector();

        // Datos leídos
        private List<Pieza> _piezas = new List<Pieza>();
        private List<FilaResumen> _resumen = new List<FilaResumen>();
        private List<Observacion> _observaciones = new List<Observacion>();
        private List<string> _materiales;   // null hasta leer el catálogo

        // Controles
        private readonly Label _lblModelo = new Label();
        private readonly CheckBox _chkSoloAcero = new CheckBox { Text = "Solo acero", Checked = true, AutoSize = true };
        private readonly ProgressBar _progreso = new ProgressBar { Width = 160, Height = 14, Visible = false };
        private readonly TabControl _pestanas = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
        private readonly DataGridView _gridPiezas = Grilla();
        private readonly DataGridView _gridResumen = Grilla();
        private readonly DataGridView _gridRevision = Grilla();
        private readonly ListBox _lstMateriales = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly ListBox _lstPernos = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly Label _lblCatalogo = new Label { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ToolStripStatusLabel _estado = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ToolStripStatusLabel _totales = new ToolStripStatusLabel();
        private readonly List<Button> _botones = new List<Button>();

        public MainForm()
        {
            Text = Program.Nombre;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1150, 680);
            MinimumSize = new Size(900, 500);

            Controls.Add(CrearContenido());
            Controls.Add(CrearBarraHerramientas());
            Controls.Add(CrearEstado());
            Controls.Add(CrearEncabezado());

            _chkSoloAcero.CheckedChanged += (s, e) => Mostrar();
            Shown += (s, e) => ComprobarConexion();
        }

        // ================================================================== construcción

        private Control CrearEncabezado()
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Azul };
            p.Controls.Add(new Label
            {
                Text = "Piezas del modelo  ·  Tekla " + Diagnostico.VersionApi, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Color.White,
                AutoSize = true, Location = new Point(16, 7)
            });
            _lblModelo.ForeColor = AzulClaro;
            _lblModelo.AutoSize = true;
            _lblModelo.Location = new Point(18, 36);
            p.Controls.Add(_lblModelo);
            return p;
        }

        private Control CrearBarraHerramientas()
        {
            var f = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 48, Padding = new Padding(12, 9, 12, 0), BackColor = Gris, WrapContents = false
            };
            f.Controls.Add(Boton("Leer todo el modelo", async (s, e) => await Leer(seleccion: false), principal: true));
            f.Controls.Add(Boton("Leer selección", async (s, e) => await Leer(seleccion: true)));
            f.Controls.Add(Separador());
            f.Controls.Add(Boton("Seleccionar en Tekla", (s, e) => SeleccionarEnTekla()));
            f.Controls.Add(Boton("Exportar a Excel", (s, e) => Exportar()));
            f.Controls.Add(Boton("Diagnóstico", (s, e) => MostrarDiagnostico(null)));
            f.Controls.Add(Separador());
            _chkSoloAcero.Margin = new Padding(4, 6, 12, 0);
            f.Controls.Add(_chkSoloAcero);
            _progreso.Margin = new Padding(0, 9, 0, 0);
            f.Controls.Add(_progreso);
            return f;
        }

        private Control CrearContenido()
        {
            _pestanas.TabPages.Add(Pagina("Piezas", _gridPiezas,
                "Doble clic en una fila para seleccionar la pieza en Tekla."));
            _pestanas.TabPages.Add(Pagina("Resumen por perfil", _gridResumen,
                "Metrado: piezas agrupadas por perfil y material, con longitud, peso y área de pintura."));
            _pestanas.TabPages.Add(Pagina("Revisión", _gridRevision,
                "Errores de organización que conviene corregir antes de numerar. Lea el catálogo para revisar también los materiales."));
            _pestanas.TabPages.Add(PaginaCatalogo());

            _gridPiezas.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) SeleccionarEnTekla(); };
            _gridResumen.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) SeleccionarEnTekla(); };
            _gridRevision.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) SeleccionarEnTekla(); };

            var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 4) };
            p.Controls.Add(_pestanas);
            return p;
        }

        private TabPage PaginaCatalogo()
        {
            var pagina = new TabPage("Catálogo") { Padding = new Padding(8), BackColor = Color.White };
            var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tabla.Controls.Add(new Label { Text = "Materiales", Font = new Font(Font, FontStyle.Bold), AutoSize = true }, 0, 0);
            tabla.Controls.Add(new Label { Text = "Pernos (norma y diámetro)", Font = new Font(Font, FontStyle.Bold), AutoSize = true }, 1, 0);
            tabla.Controls.Add(_lstMateriales, 0, 1);
            tabla.Controls.Add(_lstPernos, 1, 1);

            var arriba = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false };
            arriba.Controls.Add(Boton("Leer catálogo", async (s, e) => await LeerCatalogo(), principal: true));
            _lblCatalogo.AutoSize = true;
            _lblCatalogo.Margin = new Padding(8, 8, 0, 0);
            _lblCatalogo.Text = "Lee los materiales, pernos y perfiles del entorno de Tekla abierto.";
            arriba.Controls.Add(_lblCatalogo);

            pagina.Controls.Add(tabla);
            pagina.Controls.Add(arriba);
            return pagina;
        }

        private Control CrearEstado()
        {
            var barra = new StatusStrip { SizingGrip = false, BackColor = Gris };
            barra.Items.Add(_estado);
            barra.Items.Add(_totales);
            return barra;
        }

        private static TabPage Pagina(string titulo, DataGridView grilla, string ayuda)
        {
            var pagina = new TabPage(titulo) { Padding = new Padding(8), BackColor = Color.White };
            pagina.Controls.Add(grilla);
            pagina.Controls.Add(new Label
            {
                Text = ayuda, Dock = DockStyle.Top, Height = 24, ForeColor = Color.FromArgb(110, 118, 129)
            });
            return pagina;
        }

        private static DataGridView Grilla()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                EnableHeadersVisualStyles = false,
            };
            g.ColumnHeadersDefaultCellStyle.BackColor = Gris;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f);
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 252);
            return g;
        }

        private Button Boton(string texto, EventHandler click, bool principal = false)
        {
            var b = new Button
            {
                Text = texto,
                AutoSize = true,
                Height = 30,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = principal ? Color.FromArgb(0, 120, 212) : Color.White,
                ForeColor = principal ? Color.White : Color.Black,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderColor = principal ? Color.FromArgb(0, 120, 212) : Borde;
            b.Click += click;
            _botones.Add(b);
            return b;
        }

        private static Control Separador() => new Label { Width = 1, Height = 28, BackColor = Color.FromArgb(210, 214, 220), Margin = new Padding(4, 1, 12, 0) };

        // ================================================================== Tekla

        /// <param name="avisar">Si no hay conexión, muestra el diagnóstico en una ventana.</param>
        private bool ComprobarConexion(bool avisar = false)
        {
            bool ok;
            try { ok = _tekla.Conectado; }
            catch (Exception ex)
            {
                // Pasa si las DLL de Tekla no coinciden con la versión abierta.
                _lblModelo.Text = "No se pudo usar la Open API de Tekla: " + ex.Message;
                if (avisar) MostrarDiagnostico(ex);
                return false;
            }

            if (!ok)
            {
                _lblModelo.Text = "No se pudo conectar con Tekla Structures " + Diagnostico.VersionApi + ". Pulse \"Diagnóstico\" para ver la causa.";
                Estado("Abra un modelo en Tekla Structures " + Diagnostico.VersionApi + " y pulse \"Leer todo el modelo\".");
                if (avisar) MostrarDiagnostico(null);
                return false;
            }
            _lblModelo.Text = "Modelo: " + _tekla.NombreModelo + "   ·   " + _tekla.CarpetaModelo;
            Estado("Conectado a Tekla.");
            return true;
        }

        private async Task Leer(bool seleccion)
        {
            if (!ComprobarConexion(avisar: true)) return;

            Ocupado(true);
            Estado(seleccion ? "Leyendo las piezas seleccionadas…" : "Leyendo todas las piezas del modelo…");
            var progreso = new Progress<int>(v => _progreso.Value = Math.Max(0, Math.Min(100, v)));
            try
            {
                // La lectura se hace en segundo plano para que la ventana no se congele en modelos grandes.
                _piezas = await Task.Run(() => seleccion ? _tekla.LeerSeleccion(progreso) : _tekla.LeerTodo(progreso));
                Mostrar();
                if (_piezas.Count == 0)
                    Estado(seleccion ? "No hay piezas seleccionadas en Tekla." : "El modelo no tiene piezas.");
            }
            catch (Exception ex)
            {
                Estado("Error al leer el modelo: " + ex.Message);
            }
            finally
            {
                Ocupado(false);
            }
        }

        private async Task LeerCatalogo()
        {
            if (!ComprobarConexion(avisar: true)) return;
            Ocupado(true);
            Estado("Leyendo el catálogo…");
            try
            {
                List<string> materiales = null, pernos = null;
                int perfiles = 0;
                await Task.Run(() =>
                {
                    materiales = TeklaLector.Materiales();
                    pernos = TeklaLector.Pernos();
                    perfiles = TeklaLector.CantidadPerfiles();
                });
                _materiales = materiales;
                _lstMateriales.DataSource = materiales;
                _lstPernos.DataSource = pernos;
                _lblCatalogo.Text = $"{materiales.Count} materiales   ·   {pernos.Count} pernos   ·   {perfiles} perfiles";
                Mostrar();
                Estado("Catálogo leído. La revisión ahora también comprueba los materiales.");
            }
            catch (Exception ex)
            {
                Estado("Error al leer el catálogo: " + ex.Message);
            }
            finally
            {
                Ocupado(false);
            }
        }

        private void SeleccionarEnTekla()
        {
            List<int> ids = IdsSeleccionados();
            if (ids.Count == 0)
            {
                Estado("Seleccione una o varias filas.");
                return;
            }
            try
            {
                int n = _tekla.Seleccionar(ids);
                Estado($"{n} piezas seleccionadas en Tekla.");
            }
            catch (Exception ex)
            {
                Estado("No se pudo seleccionar en Tekla: " + ex.Message);
            }
        }

        /// <summary>IDs de las piezas de las filas seleccionadas en la pestaña activa.</summary>
        private List<int> IdsSeleccionados()
        {
            var ids = new List<int>();
            if (_pestanas.SelectedIndex == 1)
            {
                // En el resumen, una fila es un perfil + material: se seleccionan todas sus piezas.
                foreach (DataGridViewRow r in _gridResumen.SelectedRows)
                {
                    string perfil = Convert.ToString(r.Cells["Perfil"].Value);
                    string material = Convert.ToString(r.Cells["Material"].Value);
                    ids.AddRange(Visibles().Where(p => p.Perfil.Trim() == perfil && p.Material.Trim() == material).Select(p => p.Id));
                }
            }
            else
            {
                DataGridView g = _pestanas.SelectedIndex == 2 ? _gridRevision : _gridPiezas;
                foreach (DataGridViewRow r in g.SelectedRows)
                    if (r.Cells["ID"].Value is int id) ids.Add(id);
            }
            return ids.Distinct().ToList();
        }

        // ================================================================== mostrar

        /// <summary>Piezas que se muestran según el filtro "Solo acero".</summary>
        private List<Pieza> Visibles() => _chkSoloAcero.Checked ? _piezas.Where(p => p.EsAcero).ToList() : _piezas;

        private void Mostrar()
        {
            List<Pieza> piezas = Visibles();
            _resumen = Resumen.PorPerfil(piezas);
            _observaciones = Revision.Revisar(piezas, _materiales);

            _gridPiezas.DataSource = TablaPiezas(piezas);
            _gridResumen.DataSource = TablaResumen(_resumen);
            _gridRevision.DataSource = TablaRevision(_observaciones);
            Formato(_gridPiezas);
            Formato(_gridResumen);
            Formato(_gridRevision);

            _pestanas.TabPages[2].Text = _observaciones.Count == 0 ? "Revisión" : $"Revisión ({_observaciones.Count})";
            _totales.Text = $"{piezas.Count} piezas   ·   {Resumen.PesoTotal(piezas):N1} kg   ·   {Resumen.AreaTotal(piezas):N2} m² de pintura";
            Estado($"{piezas.Count} piezas leídas.");
        }

        private static DataTable TablaPiezas(IEnumerable<Pieza> piezas)
        {
            var t = new DataTable();
            t.Columns.Add("ID", typeof(int));
            t.Columns.Add("Nombre");
            t.Columns.Add("Perfil");
            t.Columns.Add("Tipo");
            t.Columns.Add("Material");
            t.Columns.Add("Clase");
            t.Columns.Add("Prefijo pieza");
            t.Columns.Add("Prefijo conjunto");
            t.Columns.Add("Marca pieza");
            t.Columns.Add("Marca conjunto");
            t.Columns.Add("Fase", typeof(int));
            t.Columns.Add("Longitud (mm)", typeof(double));
            t.Columns.Add("Peso (kg)", typeof(double));
            t.Columns.Add("Área (m²)", typeof(double));
            foreach (Pieza p in piezas)
                t.Rows.Add(p.Id, p.Nombre, p.Perfil, Revision.NombreTipoPerfil(p.TipoPerfil), p.Material, p.Clase,
                    p.PrefijoPieza, p.PrefijoConjunto, p.MarcaPieza, p.MarcaConjunto, p.Fase,
                    p.LongitudMm, p.PesoKg, p.AreaM2);
            return t;
        }

        private static DataTable TablaResumen(IEnumerable<FilaResumen> filas)
        {
            var t = new DataTable();
            t.Columns.Add("Perfil");
            t.Columns.Add("Tipo");
            t.Columns.Add("Material");
            t.Columns.Add("Cantidad", typeof(int));
            t.Columns.Add("Longitud total (m)", typeof(double));
            t.Columns.Add("Peso total (kg)", typeof(double));
            t.Columns.Add("Área total (m²)", typeof(double));
            foreach (FilaResumen f in filas)
                t.Rows.Add(f.Perfil, Revision.NombreTipoPerfil(f.TipoPerfil), f.Material, f.Cantidad,
                    f.LongitudTotalM, f.PesoTotalKg, f.AreaTotalM2);
            return t;
        }

        private static DataTable TablaRevision(IEnumerable<Observacion> obs)
        {
            var t = new DataTable();
            t.Columns.Add("ID", typeof(int));
            t.Columns.Add("Problema");
            t.Columns.Add("Nombre");
            t.Columns.Add("Perfil");
            t.Columns.Add("Material");
            t.Columns.Add("Prefijo pieza");
            t.Columns.Add("Prefijo conjunto");
            foreach (Observacion o in obs)
                t.Rows.Add(o.Pieza.Id, o.Problema, o.Pieza.Nombre, o.Pieza.Perfil, o.Pieza.Material,
                    o.Pieza.PrefijoPieza, o.Pieza.PrefijoConjunto);
            return t;
        }

        /// <summary>Números alineados a la derecha y con decimales según la columna.</summary>
        private static void Formato(DataGridView g)
        {
            foreach (DataGridViewColumn c in g.Columns)
            {
                if (c.ValueType == typeof(double))
                {
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    c.DefaultCellStyle.Format = c.Name.StartsWith("Longitud (mm)") ? "N0"
                        : c.Name.StartsWith("Área") ? "N3" : c.Name.StartsWith("Peso total") ? "N1" : "N2";
                }
                else if (c.ValueType == typeof(int))
                {
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
        }

        // ================================================================== exportar

        private void Exportar()
        {
            if (_piezas.Count == 0)
            {
                Estado("Primero lea el modelo.");
                return;
            }
            string[] nombres = { "piezas", "resumen", "revision", "piezas" };
            int i = Math.Max(0, _pestanas.SelectedIndex);
            using (var dlg = new SaveFileDialog
            {
                Filter = "CSV para Excel (*.csv)|*.csv",
                FileName = (_tekla.NombreModelo == "" ? "modelo" : _tekla.NombreModelo) + "_" + nombres[i] + ".csv",
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                CultureInfo c = CultureInfo.CurrentCulture;
                string contenido =
                    i == 1 ? Csv.Resumen(_resumen, c)
                    : i == 2 ? Csv.Observaciones(_observaciones, c)
                    : Csv.Piezas(Visibles(), c);
                try
                {
                    Csv.Guardar(dlg.FileName, contenido);
                    Estado("Exportado: " + dlg.FileName);
                    System.Diagnostics.Process.Start(dlg.FileName);
                }
                catch (Exception ex)
                {
                    Estado("No se pudo guardar: " + ex.Message);
                }
            }
        }

        // ================================================================== diagnóstico

        private void MostrarDiagnostico(Exception error)
        {
            string texto = Diagnostico.Informe(error);
            try { Clipboard.SetText(texto); } catch { /* el portapapeles puede estar ocupado */ }
            MessageBox.Show(this, texto + "\n(Este texto ya se copió al portapapeles.)",
                Program.Nombre + " - Diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ================================================================== ayudas

        private void Estado(string texto) => _estado.Text = texto;

        private void Ocupado(bool si)
        {
            foreach (Button b in _botones) b.Enabled = !si;
            _progreso.Value = 0;
            _progreso.Visible = si;
            Cursor = si ? Cursors.WaitCursor : Cursors.Default;
        }
    }
}
