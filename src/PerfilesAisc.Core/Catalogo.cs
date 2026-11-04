using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PerfilesAisc.Core
{
    /// <summary>Un nodo del árbol: grupo (Perfiles I), serie (W), tamaño (W6) o perfil (W6X8.5).</summary>
    public sealed class Nodo
    {
        public string Texto { get; set; } = "";
        public Forma? Forma { get; set; }
        public Perfil Perfil { get; set; }
        public List<Nodo> Hijos { get; } = new List<Nodo>();
        public bool EsPerfil => Perfil != null;

        /// <summary>Todos los perfiles debajo de este nodo.</summary>
        public IEnumerable<Perfil> Perfiles()
        {
            if (Perfil != null) yield return Perfil;
            foreach (Nodo h in Hijos)
                foreach (Perfil p in h.Perfiles()) yield return p;
        }
    }

    /// <summary>
    /// Catálogo de perfiles AISC incrustado en el programa, organizado en un árbol
    /// con los mismos grupos que el catálogo de Tekla: Perfiles I → W → W6 → W6X8.5.
    /// </summary>
    public sealed class Catalogo
    {
        public List<Perfil> Perfiles { get; } = new List<Perfil>();
        public List<Nodo> Arbol { get; } = new List<Nodo>();

        private readonly Dictionary<string, Perfil> _porNombre = new Dictionary<string, Perfil>(StringComparer.OrdinalIgnoreCase);

        private static Catalogo _instancia;
        /// <summary>Catálogo cargado del recurso incrustado (se carga una sola vez).</summary>
        public static Catalogo Instancia => _instancia ?? (_instancia = CargarIncrustado());

        public Perfil Buscar(string nombre)
            => nombre != null && _porNombre.TryGetValue(nombre.Trim(), out Perfil p) ? p : null;

        /// <summary>Perfiles cuyo nombre contiene el texto (o cumple el patrón con * y ?).</summary>
        public List<Perfil> Filtrar(string filtro)
        {
            filtro = (filtro ?? "").Trim();
            if (filtro == "" || filtro == "*") return Perfiles.ToList();
            if (filtro.Contains("*") || filtro.Contains("?"))
            {
                string patron = "^" + System.Text.RegularExpressions.Regex.Escape(filtro).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                var re = new System.Text.RegularExpressions.Regex(patron, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return Perfiles.Where(p => re.IsMatch(p.Nombre)).ToList();
            }
            return Perfiles.Where(p => p.Nombre.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        /// <summary>Árbol con solo los perfiles que cumplen el filtro (vacío si ninguno).</summary>
        public List<Nodo> ArbolFiltrado(string filtro)
        {
            var permitidos = new HashSet<Perfil>(Filtrar(filtro));
            return Arbol.Select(n => Podar(n, permitidos)).Where(n => n != null).ToList();
        }

        private static Nodo Podar(Nodo n, HashSet<Perfil> permitidos)
        {
            if (n.EsPerfil) return permitidos.Contains(n.Perfil) ? n : null;
            var copia = new Nodo { Texto = n.Texto, Forma = n.Forma };
            foreach (Nodo h in n.Hijos)
            {
                Nodo hc = Podar(h, permitidos);
                if (hc != null) copia.Hijos.Add(hc);
            }
            return copia.Hijos.Count > 0 ? copia : null;
        }

        // ------------------------------------------------------------------ carga

        public static Catalogo CargarIncrustado()
        {
            Assembly asm = typeof(Catalogo).Assembly;
            using (Stream s = asm.GetManifestResourceStream("perfiles_aisc.csv"))
            using (var r = new StreamReader(s))
                return Cargar(r);
        }

        public static Catalogo Cargar(TextReader lector)
        {
            var c = new Catalogo();
            string[] columnas = lector.ReadLine().Split(',');
            string linea;
            while ((linea = lector.ReadLine()) != null)
            {
                if (linea.Trim() == "") continue;
                string[] v = linea.Split(',');
                // Algunos nombres del Excel traen "x" minúscula (W18x192); el nombre AISC usa "X".
                var p = new Perfil { Nombre = v[1].Trim().Replace('x', 'X'), Forma = FormaDe(v[0]) };
                p.Serie = Perfil.SerieDe(p.Nombre);
                for (int i = 2; i < columnas.Length && i < v.Length; i++)
                    if (v[i] != "" && double.TryParse(v[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        p.Valores[columnas[i]] = d;
                c.Perfiles.Add(p);
                c._porNombre[p.Nombre] = p;
            }
            c.ConstruirArbol();
            return c;
        }

        private static Forma FormaDe(string grupo)
        {
            switch (grupo)
            {
                case "I": return Forma.I;
                case "C": return Forma.Canal;
                case "T": return Forma.T;
                case "L": return Forma.Angulo;
                case "2L": return Forma.AnguloDoble;
                case "HSSR": return Forma.TuboRectangular;
                default: return Forma.TuboRedondo;
            }
        }

        /// <summary>Nombre en español de cada grupo del árbol.</summary>
        public static string NombreForma(Forma f)
        {
            switch (f)
            {
                case Forma.I: return "Perfiles I";
                case Forma.Canal: return "Canales";
                case Forma.T: return "Perfiles T";
                case Forma.Angulo: return "Ángulos";
                case Forma.AnguloDoble: return "Ángulos dobles";
                case Forma.TuboRectangular: return "Tubos rectangulares";
                default: return "Tubos redondos";
            }
        }

        /// <summary>Orden de las series dentro de cada grupo, como en Tekla.</summary>
        private static readonly string[] OrdenSeries = { "HP", "W", "M", "S", "C", "MC", "MT", "ST", "WT", "L", "2L", "HSS", "Pipe" };

        private void ConstruirArbol()
        {
            Arbol.Clear();
            foreach (Forma forma in new[] { Forma.I, Forma.Angulo, Forma.AnguloDoble, Forma.Canal, Forma.T, Forma.TuboRedondo, Forma.TuboRectangular })
            {
                var grupo = new Nodo { Texto = NombreForma(forma), Forma = forma };
                foreach (var serie in Perfiles.Where(p => p.Forma == forma)
                             .GroupBy(p => p.Serie)
                             .OrderBy(g => Array.IndexOf(OrdenSeries, g.Key) < 0 ? 99 : Array.IndexOf(OrdenSeries, g.Key)))
                {
                    var nodoSerie = new Nodo { Texto = serie.Key, Forma = forma };
                    foreach (var tamano in serie.GroupBy(p => Perfil.TamanoDe(p.Nombre))
                                 .OrderBy(g => Perfil.NumeroDe(g.Key)).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        var nodoTamano = new Nodo { Texto = tamano.Key, Forma = forma };
                        foreach (Perfil p in tamano.OrderBy(OrdenDentroDelTamano).ThenBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase))
                            nodoTamano.Hijos.Add(new Nodo { Texto = p.Nombre, Forma = forma, Perfil = p });
                        // Un tamaño con un solo perfil no necesita nivel propio (por ejemplo Pipe).
                        if (serie.Key == "Pipe") nodoSerie.Hijos.AddRange(nodoTamano.Hijos);
                        else nodoSerie.Hijos.Add(nodoTamano);
                    }
                    grupo.Hijos.Add(nodoSerie);
                }
                if (grupo.Hijos.Count > 0) Arbol.Add(grupo);
            }
        }

        /// <summary>Dentro de un tamaño: por peso en I, C y T; por segundo lado y espesor en L y HSS.</summary>
        private static double OrdenDentroDelTamano(Perfil p)
        {
            switch (p.Forma)
            {
                case Forma.I:
                case Forma.Canal:
                case Forma.T:
                    return p.PesoLbFt ?? 0;
                default:
                    // Segundo número del nombre (lado o ancho), luego el espesor.
                    string[] partes = p.Nombre.Split('X');
                    double b = partes.Length > 1 ? Perfil.Fraccion(partes[1]) : 0;
                    double t = partes.Length > 2 ? Perfil.Fraccion(partes[2]) : (p.V("tnom") ?? 0);
                    return -b * 1000 - t;   // de mayor a menor, como lista Tekla
            }
        }
    }
}
