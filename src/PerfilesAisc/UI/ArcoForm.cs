using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace PerfilesAisc.UI
{
    /// <summary>Datos de una viga curva: flecha del arco y plano (vertical, como un arco de techo, u horizontal, en planta).</summary>
    internal sealed class ArcoForm : Form
    {
        private readonly TextBox _flecha = new TextBox { Width = 90, Text = "2" };
        private readonly RadioButton _vertical = new RadioButton { Text = "Vertical: arco de techo (la flecha sube)", AutoSize = true, Checked = true };
        private readonly RadioButton _horizontal = new RadioButton { Text = "Horizontal: curva en planta (la flecha va a la izquierda del sentido)", AutoSize = true };

        /// <summary>Flecha en metros.</summary>
        public double FlechaM { get; private set; } = 2;
        public bool Vertical => _vertical.Checked;

        public ArcoForm(string perfil, double flechaInicial)
        {
            Text = "Viga curva " + perfil;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 190);
            _flecha.Text = flechaInicial.ToString("0.###", CultureInfo.CurrentCulture);

            Controls.Add(new Label { Text = "Elija los dos extremos de la viga en el modelo. La viga pasará por un arco con esta flecha:", AutoSize = true, MaximumSize = new Size(410, 0), Location = new Point(16, 12) });
            Controls.Add(new Label { Text = "Flecha del arco (m):", AutoSize = true, Location = new Point(16, 56) });
            _flecha.Location = new Point(150, 53);
            Controls.Add(_flecha);
            _vertical.Location = new Point(16, 86);
            _horizontal.Location = new Point(16, 110);
            Controls.Add(_vertical);
            Controls.Add(_horizontal);

            var ok = new Button { Text = "Aceptar", Location = new Point(260, 148), Width = 80, DialogResult = DialogResult.None };
            ok.Click += (s, e) =>
            {
                if (!double.TryParse(_flecha.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double f) &&
                    !double.TryParse(_flecha.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out f) || f <= 0)
                {
                    MessageBox.Show(this, "Escriba la flecha en metros, mayor que cero.", App.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                FlechaM = f;
                DialogResult = DialogResult.OK;
                Close();
            };
            var cancelar = new Button { Text = "Cancelar", Location = new Point(348, 148), Width = 80, DialogResult = DialogResult.Cancel };
            Controls.Add(ok);
            Controls.Add(cancelar);
            AcceptButton = ok;
            CancelButton = cancelar;
        }
    }
}
