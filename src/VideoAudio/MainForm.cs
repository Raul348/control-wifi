using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoAudio.Core;

namespace VideoAudio
{
    /// <summary>Ventana principal: arrastrar videos, elegir formato y opciones, y convertir.</summary>
    internal sealed class MainForm : Form
    {
        private static readonly Color Azul = Color.FromArgb(31, 56, 100);
        private static readonly Color AzulClaro = Color.FromArgb(205, 215, 230);
        private static readonly Color Gris = Color.FromArgb(110, 118, 129);
        private static readonly Color Fondo = Color.FromArgb(247, 248, 250);
        private static readonly Color Verde = Color.FromArgb(16, 137, 62);
        private static readonly Color Rojo = Color.FromArgb(196, 43, 28);
        private static readonly Color Ambar = Color.FromArgb(180, 110, 0);

        private readonly ListView _lista = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false,
            AllowDrop = true, BorderStyle = BorderStyle.FixedSingle, HideSelection = false
        };
        private readonly Label _soltar = new Label
        {
            Text = "Arrastre aquí sus videos\n\no use el botón «Añadir videos»",
            TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, ForeColor = Gris,
            Font = new Font("Segoe UI", 12f), AllowDrop = true, BackColor = Color.White
        };
        private readonly ComboBox _formato = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        private readonly ComboBox _calidad = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        private readonly CheckBox _audio = new CheckBox { Text = "Sacar el audio", AutoSize = true, Checked = true };
        private readonly CheckBox _transcribir = new CheckBox { Text = "Transcribir a texto (.txt y subtítulos .srt)", AutoSize = true };
        private readonly ComboBox _modelo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
        private readonly ComboBox _idioma = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
        private readonly RadioButton _mismaCarpeta = new RadioButton { Text = "En la misma carpeta de cada video", AutoSize = true, Checked = true };
        private readonly RadioButton _otraCarpeta = new RadioButton { Text = "En:", AutoSize = true };
        private readonly TextBox _carpeta = new TextBox { Width = 230 };
        private readonly Button _convertir;
        private readonly Button _cancelar;
        private readonly ProgressBar _barra = new ProgressBar { Dock = DockStyle.Fill, Maximum = 1000 };
        private readonly Label _estado = new Label { AutoSize = true, ForeColor = Gris };
        private readonly List<Control> _bloquear = new List<Control>();
        private CancellationTokenSource _cts;

        public MainForm(string[] archivos)
        {
            Text = Program.Nombre;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(940, 600);
            MinimumSize = new Size(820, 520);
            Icon = Icon.FromHandle(Icono(32).GetHicon());

            _convertir = Boton("Convertir", async (s, e) => await Convertir(), principal: true);
            _cancelar = Boton("Cancelar", (s, e) => _cts?.Cancel());
            _cancelar.Enabled = false;

            Controls.Add(CrearCentro());
            Controls.Add(CrearOpciones());
            Controls.Add(CrearAbajo());
            Controls.Add(CrearEncabezado());

            foreach (Formato f in Enum.GetValues(typeof(Formato))) _formato.Items.Add(Formatos.Nombre(f));
            _formato.SelectedIndex = 0;
            foreach (int k in Formatos.Calidades) _calidad.Items.Add(k + " kbps");
            _calidad.SelectedIndex = 1;
            _formato.SelectedIndexChanged += (s, e) => _calidad.Enabled = Formatos.UsaCalidad(FormatoElegido);
            foreach (ModeloWhisper m in ModeloWhisper.Todos) _modelo.Items.Add(m);
            _modelo.SelectedIndex = 1;
            foreach (Idioma i in Idioma.Todos) _idioma.Items.Add(i);
            _idioma.SelectedIndex = 0;
            _transcribir.CheckedChanged += (s, e) => { _modelo.Enabled = _idioma.Enabled = _transcribir.Checked; };
            _modelo.Enabled = _idioma.Enabled = false;

            _lista.Columns.Add("Archivo", 330);
            _lista.Columns.Add("Duración", 80, HorizontalAlignment.Right);
            _lista.Columns.Add("Estado", 140);
            _lista.Columns.Add("Resultado", 360);
            foreach (Control c in new Control[] { _lista, _soltar })
            {
                c.DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                c.DragDrop += (s, e) => Agregar((string[])e.Data.GetData(DataFormats.FileDrop));
            }
            _lista.DoubleClick += (s, e) => AbrirResultado();
            _lista.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) Quitar(); };

            if (archivos != null && archivos.Length > 0) Agregar(archivos);
            Actualizar();
        }

        // ================================================================== construcción

        private Control CrearEncabezado()
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Azul };
            p.Controls.Add(new PictureBox { Image = Icono(40, Color.White), Size = new Size(40, 40), Location = new Point(16, 12) });
            p.Controls.Add(new Label { Text = Program.Nombre, Font = new Font("Segoe UI Semibold", 14f), ForeColor = Color.White, AutoSize = true, Location = new Point(66, 8) });
            p.Controls.Add(new Label
            {
                Text = "Saque el audio de sus videos y, si quiere, el texto de lo que se dice. Todo en su PC, sin internet después de la primera vez.",
                ForeColor = AzulClaro, AutoSize = true, Location = new Point(68, 38)
            });
            return p;
        }

        private Control CrearCentro()
        {
            var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 6, 6) };
            var botones = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false };
            botones.Controls.Add(Boton("Añadir videos…", (s, e) => Elegir()));
            botones.Controls.Add(Boton("Quitar", (s, e) => Quitar()));
            botones.Controls.Add(Boton("Limpiar lista", (s, e) => { _lista.Items.Clear(); Actualizar(); }));
            botones.Controls.Add(Boton("Abrir carpeta", (s, e) => AbrirCarpeta()));
            var contenedor = new Panel { Dock = DockStyle.Fill };
            contenedor.Controls.Add(_lista);
            contenedor.Controls.Add(_soltar);
            p.Controls.Add(contenedor);
            p.Controls.Add(botones);
            return p;
        }

        private Control CrearOpciones()
        {
            var p = new Panel { Dock = DockStyle.Right, Width = 290, BackColor = Fondo, Padding = new Padding(14, 10, 12, 10) };
            var f = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

            f.Controls.Add(Titulo("Audio"));
            f.Controls.Add(_audio);
            f.Controls.Add(Etiqueta("Formato"));
            f.Controls.Add(_formato);
            f.Controls.Add(Etiqueta("Calidad"));
            f.Controls.Add(_calidad);

            f.Controls.Add(Titulo("Texto"));
            f.Controls.Add(_transcribir);
            f.Controls.Add(Etiqueta("Precisión"));
            f.Controls.Add(_modelo);
            f.Controls.Add(Etiqueta("Idioma"));
            f.Controls.Add(_idioma);
            f.Controls.Add(new Label
            {
                Text = "Usa Whisper en su PC. La primera vez descarga el modelo elegido. «Bueno» es rápido y preciso en español.",
                ForeColor = Gris, AutoSize = true, MaximumSize = new Size(250, 0), Margin = new Padding(0, 4, 0, 0)
            });

            f.Controls.Add(Titulo("Guardar"));
            f.Controls.Add(_mismaCarpeta);
            var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            fila.Controls.Add(_otraCarpeta);
            fila.Controls.Add(_carpeta);
            f.Controls.Add(fila);
            var examinar = Boton("Elegir carpeta…", (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = "Carpeta donde se guardarán los audios y textos" })
                    if (d.ShowDialog(this) == DialogResult.OK) { _carpeta.Text = d.SelectedPath; _otraCarpeta.Checked = true; }
            });
            examinar.Margin = new Padding(0, 4, 0, 0);
            f.Controls.Add(examinar);
            f.Controls.Add(new Label
            {
                Text = "Nunca se reemplaza ningún archivo: si el nombre ya existe, se agrega (2), (3)...",
                ForeColor = Gris, AutoSize = true, MaximumSize = new Size(250, 0), Margin = new Padding(0, 8, 0, 0)
            });

            _bloquear.AddRange(new Control[] { _audio, _formato, _calidad, _transcribir, _modelo, _idioma, _mismaCarpeta, _otraCarpeta, _carpeta, examinar });
            p.Controls.Add(f);
            return p;
        }

        private Control CrearAbajo()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 70, ColumnCount = 3, BackColor = Fondo, Padding = new Padding(12, 8, 12, 8) };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            p.Controls.Add(_estado, 0, 0);
            p.Controls.Add(_barra, 0, 1);
            p.Controls.Add(_cancelar, 1, 1);
            p.Controls.Add(_convertir, 2, 1);
            p.SetRowSpan(_cancelar, 1);
            return p;
        }

        private static Label Titulo(string t) => new Label { Text = t, AutoSize = true, Font = new Font("Segoe UI Semibold", 10f), Margin = new Padding(0, 10, 0, 4) };
        private static Label Etiqueta(string t) => new Label { Text = t, AutoSize = true, ForeColor = Gris, Margin = new Padding(0, 6, 0, 2) };

        private Button Boton(string texto, EventHandler click, bool principal = false)
        {
            var b = new Button
            {
                Text = texto, AutoSize = true, Height = 30, Padding = new Padding(10, 0, 10, 0), Margin = new Padding(0, 4, 8, 0),
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                BackColor = principal ? Color.FromArgb(0, 120, 212) : Color.White,
                ForeColor = principal ? Color.White : Color.Black,
                Font = principal ? new Font("Segoe UI Semibold", 10f) : Font,
            };
            b.FlatAppearance.BorderColor = principal ? Color.FromArgb(0, 120, 212) : Color.FromArgb(200, 204, 210);
            b.Click += click;
            if (!principal && texto != "Cancelar") _bloquear.Add(b);
            return b;
        }

        /// <summary>Ícono: nota musical sobre un fotograma.</summary>
        private static Bitmap Icono(int t, Color? color = null)
        {
            Color c = color ?? Color.FromArgb(0, 120, 212);
            var bmp = new Bitmap(t, t);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float m = t * 0.08f;
                using (var p = new Pen(c, Math.Max(1.5f, t / 16f)))
                    g.DrawRectangle(p, m, m + t * 0.08f, t - 2 * m, t - 2 * m - t * 0.16f);
                using (var b = new SolidBrush(c))
                {
                    float r = t * 0.13f;
                    g.FillEllipse(b, t * 0.30f - r, t * 0.68f - r * 0.8f, 2 * r, 1.6f * r);
                    g.FillEllipse(b, t * 0.64f - r, t * 0.60f - r * 0.8f, 2 * r, 1.6f * r);
                    g.FillRectangle(b, t * 0.30f + r - t * 0.035f, t * 0.28f, t * 0.07f, t * 0.40f);
                    g.FillRectangle(b, t * 0.64f + r - t * 0.035f, t * 0.22f, t * 0.07f, t * 0.38f);
                    g.FillPolygon(b, new[] { new PointF(t * 0.30f + r - t * 0.035f, t * 0.28f), new PointF(t * 0.64f + r + t * 0.035f, t * 0.20f),
                                             new PointF(t * 0.64f + r + t * 0.035f, t * 0.28f), new PointF(t * 0.30f + r - t * 0.035f, t * 0.36f) });
                }
            }
            return bmp;
        }

        // ================================================================== lista

        private void Elegir()
        {
            using (var d = new OpenFileDialog
            {
                Multiselect = true, Title = "Elija los videos",
                Filter = "Videos y audios|" + string.Join(";", Rutas.Extensiones.Select(x => "*" + x)) + "|Todos los archivos|*.*"
            })
                if (d.ShowDialog(this) == DialogResult.OK) Agregar(d.FileNames);
        }

        private void Agregar(IEnumerable<string> rutas)
        {
            var nuevos = new List<string>();
            foreach (string r in rutas)
            {
                if (Directory.Exists(r))
                    nuevos.AddRange(Directory.EnumerateFiles(r, "*.*", SearchOption.AllDirectories).Where(Rutas.Aceptado));
                else if (File.Exists(r) && Rutas.Aceptado(r)) nuevos.Add(r);
            }
            var ya = new HashSet<string>(_lista.Items.Cast<ListViewItem>().Select(i => (string)i.Tag), StringComparer.OrdinalIgnoreCase);
            foreach (string r in nuevos.Where(r => !ya.Contains(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var it = new ListViewItem(Path.GetFileName(r)) { Tag = r, ToolTipText = r };
                it.SubItems.Add("");
                it.SubItems.Add("Pendiente");
                it.SubItems.Add("");
                _lista.Items.Add(it);
            }
            Actualizar();
        }

        private void Quitar()
        {
            if (_cts != null) return;
            foreach (ListViewItem it in _lista.SelectedItems) _lista.Items.Remove(it);
            Actualizar();
        }

        private void Actualizar()
        {
            _soltar.Visible = _lista.Items.Count == 0;
            _lista.Visible = !_soltar.Visible;
            _convertir.Enabled = _cts == null && _lista.Items.Count > 0;
            if (_cts == null) _estado.Text = _lista.Items.Count == 0 ? "Sin archivos." : _lista.Items.Count + " archivo(s) en la lista.";
        }

        private void AbrirResultado()
        {
            if (_lista.SelectedItems.Count == 0) return;
            string r = _lista.SelectedItems[0].SubItems[3].Tag as string ?? (string)_lista.SelectedItems[0].Tag;
            if (File.Exists(r)) Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + r + "\""));
        }

        private void AbrirCarpeta()
        {
            string carpeta = _otraCarpeta.Checked && Directory.Exists(_carpeta.Text) ? _carpeta.Text
                : _lista.SelectedItems.Count > 0 ? Path.GetDirectoryName((string)_lista.SelectedItems[0].Tag)
                : _lista.Items.Count > 0 ? Path.GetDirectoryName((string)_lista.Items[0].Tag) : null;
            if (carpeta != null && Directory.Exists(carpeta)) Process.Start("explorer.exe", "\"" + carpeta + "\"");
        }

        private static void Estado(ListViewItem it, string estado, Color color, string resultado = null, string rutaResultado = null)
        {
            it.SubItems[2].Text = estado;
            it.SubItems[2].ForeColor = color;
            it.UseItemStyleForSubItems = false;
            if (resultado != null) it.SubItems[3].Text = resultado;
            if (rutaResultado != null) it.SubItems[3].Tag = rutaResultado;
        }

        private Formato FormatoElegido => (Formato)_formato.SelectedIndex;

        // ================================================================== convertir

        private async Task Convertir()
        {
            if (!_audio.Checked && !_transcribir.Checked)
            {
                MessageBox.Show(this, "Marque «Sacar el audio», «Transcribir a texto» o ambas.", Program.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string carpeta = null;
            if (_otraCarpeta.Checked)
            {
                carpeta = _carpeta.Text.Trim();
                if (carpeta == "" || !Directory.Exists(carpeta))
                {
                    MessageBox.Show(this, "La carpeta de destino no existe.", Program.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _cts = new CancellationTokenSource();
            Bloquear(true);
            try
            {
                var conversor = new Conversor();
                if (!await Preparar(conversor, _cts.Token)) return;

                var items = _lista.Items.Cast<ListViewItem>().ToList();
                int hechos = 0, errores = 0;
                Formato formato = FormatoElegido;
                int kbps = Formatos.Calidades[_calidad.SelectedIndex];
                string idioma = ((Idioma)_idioma.SelectedItem).Codigo;
                int pasos = (_audio.Checked ? 1 : 0) + (_transcribir.Checked ? 1 : 0);

                for (int i = 0; i < items.Count; i++)
                {
                    ListViewItem it = items[i];
                    string video = (string)it.Tag;
                    _lista.EnsureVisible(it.Index);
                    var partes = new List<string>();
                    string ruta = null;
                    bool ok = true;
                    int paso = 0;

                    void Barra(double pct, string que)
                    {
                        double total = (i + (paso + pct / 100.0) / pasos) / items.Count;
                        _barra.Value = (int)Math.Max(0, Math.Min(1000, total * 1000));
                        it.SubItems[2].Text = que + " " + (int)pct + " %";
                    }

                    try
                    {
                        if (_audio.Checked)
                        {
                            Estado(it, "Audio 0 %", Color.Black);
                            _estado.Text = "Sacando el audio de " + it.Text + "…";
                            Resultado r = await conversor.Convertir(video, carpeta, formato, kbps, new Progress<double>(p => Barra(p, "Audio")), _cts.Token);
                            if (r.DuracionVideo != null) it.SubItems[1].Text = Progreso.Texto(r.DuracionVideo.Value);
                            if (r.Ok)
                            {
                                partes.Add(Path.GetFileName(r.Audio) + (r.DuracionAudio != null ? " (" + Progreso.Texto(r.DuracionAudio.Value) + ")" : ""));
                                ruta = r.Audio;
                                if (r.Mensaje != "") { partes.Add(r.Mensaje); ok = false; }
                            }
                            else { partes.Add(r.Mensaje); ok = false; }
                            paso++;
                        }
                        if (_transcribir.Checked)
                        {
                            Estado(it, "Texto 0 %", Color.Black);
                            _estado.Text = "Transcribiendo " + it.Text + "… (puede tardar varios minutos)";
                            var t = await conversor.Transcribir(video, carpeta, idioma, new Progress<double>(p => Barra(p, "Texto")), _cts.Token);
                            if (t.Ok)
                            {
                                partes.Add(Path.GetFileName(t.Txt) + (t.Srt != null ? " + .srt" : ""));
                                ruta = ruta ?? t.Txt;
                            }
                            else { partes.Add(t.Mensaje); ok = false; }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Estado(it, "Cancelado", Gris, "");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        partes.Add(ex.Message);
                        ok = false;
                    }

                    if (ok) { hechos++; Estado(it, "Listo", Verde, string.Join("  ·  ", partes), ruta); }
                    else
                    {
                        errores++;
                        bool parcial = ruta != null;
                        Estado(it, parcial ? "Revisar" : "Error", parcial ? Ambar : Rojo, string.Join("  ·  ", partes), ruta);
                    }
                }
                _barra.Value = 1000;
                _estado.Text = errores == 0 ? "Terminado: " + hechos + " archivo(s). Doble clic en una fila para ver el resultado."
                                            : "Terminado: " + hechos + " bien, " + errores + " con avisos o errores (vea la columna Resultado).";
            }
            catch (OperationCanceledException)
            {
                _estado.Text = "Cancelado. Los archivos a medias se borraron; los videos no se tocaron.";
            }
            catch (Exception ex)
            {
                _estado.Text = "Error: " + ex.Message;
                MessageBox.Show(this, ex.Message, Program.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                Bloquear(false);
                Actualizar();
            }
        }

        /// <summary>Ubica FFmpeg, Whisper y el modelo; si faltan, ofrece descargarlos.</summary>
        private async Task<bool> Preparar(Conversor c, CancellationToken ct)
        {
            var progreso = new Progress<(long Leido, long Total)>(p =>
            {
                if (p.Total > 0) _barra.Value = (int)Math.Min(1000, p.Leido * 1000 / p.Total);
                _estado.Text = _estado.Tag + "  " + (p.Leido / 1048576) + (p.Total > 0 ? " de " + (p.Total / 1048576) : "") + " MB";
            });

            c.Ffmpeg = Herramientas.Ffmpeg();
            if (c.Ffmpeg == null)
            {
                if (!Preguntar("Para convertir se necesita FFmpeg, un programa gratuito (unos 90 MB).\n\n¿Descargarlo ahora? Solo se hace una vez.")) return false;
                _estado.Tag = "Descargando FFmpeg…";
                c.Ffmpeg = await Herramientas.DescargarFfmpeg(progreso, ct);
            }

            if (_transcribir.Checked)
            {
                c.Whisper = Herramientas.Whisper();
                if (c.Whisper == null)
                {
                    if (!Preguntar("Para transcribir se necesita whisper.cpp, un programa gratuito (unos 5 MB).\n\n¿Descargarlo ahora? Solo se hace una vez.")) return false;
                    _estado.Tag = "Descargando whisper.cpp…";
                    c.Whisper = await Herramientas.DescargarWhisper(progreso, ct);
                }
                var modelo = (ModeloWhisper)_modelo.SelectedItem;
                c.Modelo = Herramientas.Modelo(modelo);
                if (c.Modelo == null)
                {
                    if (!Preguntar("Falta el modelo de voz «" + modelo.Nombre + "» (" + modelo.Megas + " MB).\n\n¿Descargarlo ahora? Solo se hace una vez.")) return false;
                    _estado.Tag = "Descargando el modelo " + modelo.Nombre + "…";
                    c.Modelo = await Herramientas.DescargarModelo(modelo, progreso, ct);
                }
            }
            _estado.Tag = null;
            _barra.Value = 0;
            return true;
        }

        private bool Preguntar(string texto)
            => MessageBox.Show(this, texto, Program.Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        private void Bloquear(bool si)
        {
            foreach (Control c in _bloquear) c.Enabled = !si;
            if (!si)
            {
                _calidad.Enabled = Formatos.UsaCalidad(FormatoElegido);
                _modelo.Enabled = _idioma.Enabled = _transcribir.Checked;
            }
            _convertir.Enabled = !si;
            _cancelar.Enabled = si;
            _lista.AllowDrop = _soltar.AllowDrop = !si;
            if (si) _barra.Value = 0;
        }
    }
}
