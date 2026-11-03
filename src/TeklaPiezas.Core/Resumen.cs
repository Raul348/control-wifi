using System;
using System.Collections.Generic;
using System.Linq;

namespace TeklaPiezas.Core
{
    /// <summary>Una fila del resumen: todas las piezas con el mismo perfil y material.</summary>
    public sealed class FilaResumen
    {
        public string Perfil { get; set; } = "";
        public string Material { get; set; } = "";
        public string TipoPerfil { get; set; } = "";
        public int Cantidad { get; set; }
        public double LongitudTotalM { get; set; }
        public double PesoTotalKg { get; set; }
        public double AreaTotalM2 { get; set; }
    }

    public static class Resumen
    {
        /// <summary>
        /// Agrupa las piezas por perfil y material, como un metrado.
        /// Ordena por tipo de perfil y luego por peso, de mayor a menor.
        /// </summary>
        public static List<FilaResumen> PorPerfil(IEnumerable<Pieza> piezas)
        {
            return piezas
                .GroupBy(p => new { Perfil = p.Perfil.Trim(), Material = p.Material.Trim() })
                .Select(g => new FilaResumen
                {
                    Perfil = g.Key.Perfil,
                    Material = g.Key.Material,
                    TipoPerfil = g.First().TipoPerfil,
                    Cantidad = g.Count(),
                    LongitudTotalM = g.Sum(p => p.LongitudMm) / 1000.0,
                    PesoTotalKg = g.Sum(p => p.PesoKg),
                    AreaTotalM2 = g.Sum(p => p.AreaM2),
                })
                .OrderBy(f => f.TipoPerfil, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(f => f.PesoTotalKg)
                .ToList();
        }

        public static double PesoTotal(IEnumerable<Pieza> piezas) => piezas.Sum(p => p.PesoKg);
        public static double AreaTotal(IEnumerable<Pieza> piezas) => piezas.Sum(p => p.AreaM2);
    }
}
