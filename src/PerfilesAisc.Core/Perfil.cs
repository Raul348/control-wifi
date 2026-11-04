using System;
using System.Collections.Generic;
using System.Globalization;

namespace PerfilesAisc.Core
{
    /// <summary>Forma de la sección. Coincide con los grupos del catálogo de Tekla.</summary>
    public enum Forma
    {
        I,              // W, M, S, HP
        Canal,          // C, MC
        T,              // WT, MT, ST
        Angulo,         // L
        AnguloDoble,    // 2L
        TuboRectangular,// HSS rectangular y cuadrado
        TuboRedondo     // HSS redondo y Pipe
    }

    /// <summary>
    /// Un perfil del catálogo AISC. Los valores se guardan en las unidades del catálogo
    /// (pulgadas, pulgadas², pulgadas⁴, libras por pie); se convierten al mostrarlos.
    /// </summary>
    public sealed class Perfil
    {
        public string Nombre { get; set; } = "";
        public Forma Forma { get; set; }
        /// <summary>Serie dentro de la forma: W, HP, M, S, C, MC, WT, MT, ST, L, 2L, HSS, Pipe.</summary>
        public string Serie { get; set; } = "";
        /// <summary>Valores por nombre de columna del catálogo: d, bf, tw, tf, k, A, Ix...</summary>
        public Dictionary<string, double> Valores { get; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Valor en unidades del catálogo, o null si el perfil no tiene esa columna.</summary>
        public double? V(string columna) => Valores.TryGetValue(columna, out double v) ? v : (double?)null;

        public bool Tiene(string columna) => Valores.ContainsKey(columna);

        // --- Medidas principales en pulgadas (null si no aplican a la forma) ---

        /// <summary>Altura total: d en I, C y T; d (lado largo) en L; h en HSS rectangular; OD en redondos.</summary>
        public double? AltoIn => V("d") ?? V("h") ?? V("OD");
        /// <summary>Ancho: bf en I, C y T; b en L y HSS; OD en redondos.</summary>
        public double? AnchoIn => V("bf") ?? V("b") ?? V("OD");
        /// <summary>Espesor de alma (tw) o espesor único (t, tdes).</summary>
        public double? AlmaIn => V("tw") ?? V("tdes") ?? V("t");
        /// <summary>Espesor de ala (tf) o espesor único.</summary>
        public double? AlaIn => V("tf") ?? V("tdes") ?? V("t");
        /// <summary>Radio del acuerdo alma-ala: k(des) - tf en perfiles laminados.</summary>
        public double? RadioIn
        {
            get
            {
                double? k = V("k"), tf = V("tf") ?? V("t");
                if (k == null || tf == null) return null;
                double r = k.Value - tf.Value;
                return r > 0 ? r : (double?)null;
            }
        }

        public double? AreaIn2 => V("A");
        public double? PesoLbFt => V("wt");

        /// <summary>Peso en kg/m.</summary>
        public double PesoKgM => Unidades.KgM(PesoLbFt ?? 0);

        public override string ToString() => Nombre;

        // --- Clasificación por el nombre, igual que las reglas del árbol de Tekla ---

        /// <summary>Serie a partir del nombre: W6X8.5 → W, HP8X36 → HP, 2L4X4X1/2 → 2L, Pipe1STD → Pipe.</summary>
        public static string SerieDe(string nombre)
        {
            if (nombre.StartsWith("Pipe", StringComparison.OrdinalIgnoreCase)) return "Pipe";
            int i = 0;
            if (nombre.StartsWith("2")) i = 1;
            while (i < nombre.Length && char.IsLetter(nombre[i])) i++;
            return nombre.Substring(0, i);
        }

        /// <summary>Tamaño nominal: W6X8.5 → W6, L2-1/2X2X1/4 → L2-1/2, HSS20X12X5/8 → HSS20, Pipe1STD → Pipe.</summary>
        public static string TamanoDe(string nombre)
        {
            if (nombre.StartsWith("Pipe", StringComparison.OrdinalIgnoreCase)) return "Pipe";
            int x = nombre.IndexOf('X');
            return x < 0 ? nombre : nombre.Substring(0, x);
        }

        /// <summary>Primer número del nombre, con fracciones: W6X8.5 → 6; L2-1/2X2 → 2.5; Pipe3/4STD → 0.75.</summary>
        public static double NumeroDe(string nombre)
        {
            string s = nombre.StartsWith("Pipe", StringComparison.OrdinalIgnoreCase) ? nombre.Substring(4) : TamanoDe(nombre);
            int i = 0;
            if (s.StartsWith("2") && s.Length > 1 && char.IsLetter(s[1])) i = 1;
            while (i < s.Length && char.IsLetter(s[i])) i++;
            int j = i;
            while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '.' || s[j] == '/' || s[j] == '-')) j++;
            return Fraccion(s.Substring(i, j - i));
        }

        /// <summary>Convierte "2-1/2", "1/2", "8.5" o "12" a número. Cero si no se puede.</summary>
        public static double Fraccion(string texto)
        {
            texto = (texto ?? "").Trim();
            if (texto == "") return 0;
            double entero = 0, fraccion = 0;
            string[] partes = texto.Split('-');
            string f = partes.Length > 1 ? partes[1] : (partes[0].Contains("/") ? partes[0] : null);
            string e = partes.Length > 1 ? partes[0] : (partes[0].Contains("/") ? null : partes[0]);
            if (e != null) double.TryParse(e, NumberStyles.Float, CultureInfo.InvariantCulture, out entero);
            if (f != null)
            {
                string[] nd = f.Split('/');
                if (nd.Length == 2
                    && double.TryParse(nd[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double n)
                    && double.TryParse(nd[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d != 0)
                    fraccion = n / d;
            }
            return entero + fraccion;
        }
    }
}
