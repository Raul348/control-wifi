using System;
using System.Collections.Generic;
using System.Linq;

namespace TeklaPiezas.Core
{
    /// <summary>Un problema encontrado en una pieza.</summary>
    public sealed class Observacion
    {
        public Pieza Pieza { get; set; }
        public string Problema { get; set; } = "";
    }

    /// <summary>
    /// Revisión antes de numerar. Busca errores de organización que después
    /// salen como marcas repetidas o confusas en los planos.
    /// </summary>
    public static class Revision
    {
        /// <param name="piezas">Piezas a revisar.</param>
        /// <param name="materialesCatalogo">Nombres de materiales del catálogo de Tekla. Null para no revisar materiales.</param>
        public static List<Observacion> Revisar(IList<Pieza> piezas, ICollection<string> materialesCatalogo = null)
        {
            var lista = new List<Observacion>();
            HashSet<string> materiales = materialesCatalogo == null
                ? null
                : new HashSet<string>(materialesCatalogo, StringComparer.OrdinalIgnoreCase);

            foreach (Pieza p in piezas)
            {
                if (string.IsNullOrWhiteSpace(p.PrefijoPieza))
                    Agregar(lista, p, "Sin prefijo de pieza");
                if (string.IsNullOrWhiteSpace(p.PrefijoConjunto))
                    Agregar(lista, p, "Sin prefijo de conjunto");
                if (string.IsNullOrWhiteSpace(p.Nombre))
                    Agregar(lista, p, "Sin nombre");
                if (p.PesoKg <= 0)
                    Agregar(lista, p, "Peso cero: el perfil \"" + p.Perfil + "\" puede no existir en el catálogo");
                if (materiales != null && !materiales.Contains(p.Material.Trim()))
                    Agregar(lista, p, "El material \"" + p.Material + "\" no está en el catálogo");
            }

            // Un mismo prefijo de pieza usado con tipos de perfil distintos (por ejemplo vigas I y ángulos)
            // hace que las marcas no digan qué tipo de pieza es.
            foreach (var g in piezas
                         .Where(p => !string.IsNullOrWhiteSpace(p.PrefijoPieza))
                         .GroupBy(p => p.PrefijoPieza.Trim(), StringComparer.Ordinal))
            {
                List<string> tipos = g.Select(p => p.TipoPerfil).Where(t => t != "")
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToList();
                if (tipos.Count > 1)
                {
                    string texto = "El prefijo \"" + g.Key + "\" se usa en perfiles de tipos distintos (" + string.Join(", ", tipos) + ")";
                    foreach (Pieza p in g) Agregar(lista, p, texto);
                }
            }

            return lista
                .OrderBy(o => o.Problema, StringComparer.CurrentCulture)
                .ThenBy(o => o.Pieza.Perfil, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void Agregar(List<Observacion> lista, Pieza p, string problema)
            => lista.Add(new Observacion { Pieza = p, Problema = problema });

        /// <summary>Nombre en español del tipo de perfil de Tekla.</summary>
        public static string NombreTipoPerfil(string tipo)
        {
            switch ((tipo ?? "").Trim().ToUpperInvariant())
            {
                case "I": return "Viga I / H";
                case "L": return "Ángulo";
                case "U": return "Canal";
                case "C": return "Canal (plegado)";
                case "Z": return "Perfil Z";
                case "T": return "Perfil T";
                case "M": return "Tubo rectangular";
                case "RO": return "Tubo redondo";
                case "RU": return "Barra redonda";
                case "B": return "Placa";
                case "": return "";
                default: return tipo;
            }
        }
    }
}
