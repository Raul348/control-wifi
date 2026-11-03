using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TeklaPiezas.Core
{
    /// <summary>
    /// Exporta a CSV que Excel abre directamente.
    /// Usa el separador de lista y el separador decimal de la configuración regional de Windows,
    /// para que Excel ponga cada dato en su columna y lea bien los números.
    /// </summary>
    public static class Csv
    {
        public static string Piezas(IEnumerable<Pieza> piezas, CultureInfo cultura)
        {
            var t = new Tabla(cultura,
                "ID", "Nombre", "Perfil", "Tipo de perfil", "Material", "Clase", "Acabado",
                "Prefijo pieza", "Prefijo conjunto", "Marca pieza", "Marca conjunto", "Fase",
                "Longitud (mm)", "Peso (kg)", "Área (m²)");
            foreach (Pieza p in piezas)
                t.Fila(p.Id, p.Nombre, p.Perfil, Revision.NombreTipoPerfil(p.TipoPerfil), p.Material, p.Clase, p.Acabado,
                    p.PrefijoPieza, p.PrefijoConjunto, p.MarcaPieza, p.MarcaConjunto, p.Fase,
                    Math.Round(p.LongitudMm, 0), Math.Round(p.PesoKg, 2), Math.Round(p.AreaM2, 3));
            return t.ToString();
        }

        public static string Resumen(IEnumerable<FilaResumen> filas, CultureInfo cultura)
        {
            var t = new Tabla(cultura,
                "Perfil", "Tipo de perfil", "Material", "Cantidad", "Longitud total (m)", "Peso total (kg)", "Área total (m²)");
            foreach (FilaResumen f in filas)
                t.Fila(f.Perfil, Revision.NombreTipoPerfil(f.TipoPerfil), f.Material, f.Cantidad,
                    Math.Round(f.LongitudTotalM, 2), Math.Round(f.PesoTotalKg, 1), Math.Round(f.AreaTotalM2, 2));
            return t.ToString();
        }

        public static string Observaciones(IEnumerable<Observacion> obs, CultureInfo cultura)
        {
            var t = new Tabla(cultura, "ID", "Problema", "Nombre", "Perfil", "Material", "Prefijo pieza", "Prefijo conjunto");
            foreach (Observacion o in obs)
                t.Fila(o.Pieza.Id, o.Problema, o.Pieza.Nombre, o.Pieza.Perfil, o.Pieza.Material, o.Pieza.PrefijoPieza, o.Pieza.PrefijoConjunto);
            return t.ToString();
        }

        /// <summary>Guarda en UTF-8 con BOM: así Excel muestra bien tildes, ñ y el símbolo ².</summary>
        public static void Guardar(string ruta, string contenido)
            => File.WriteAllText(ruta, contenido, new UTF8Encoding(true));

        /// <summary>Separador de columnas: el de Windows, salvo que coincida con el decimal.</summary>
        public static string Separador(CultureInfo cultura)
        {
            string sep = cultura.TextInfo.ListSeparator;
            if (string.IsNullOrEmpty(sep) || sep == cultura.NumberFormat.NumberDecimalSeparator)
                sep = cultura.NumberFormat.NumberDecimalSeparator == "," ? ";" : ",";
            return sep;
        }

        private sealed class Tabla
        {
            private readonly CultureInfo _cultura;
            private readonly string _sep;
            private readonly StringBuilder _sb = new StringBuilder();

            public Tabla(CultureInfo cultura, params string[] encabezados)
            {
                _cultura = cultura;
                _sep = Separador(cultura);
                Fila(encabezados.Cast<object>().ToArray());
            }

            public void Fila(params object[] valores)
            {
                _sb.Append(string.Join(_sep, valores.Select(Celda))).Append("\r\n");
            }

            private string Celda(object v)
            {
                string s;
                switch (v)
                {
                    case null: s = ""; break;
                    case double d: s = d.ToString("0.###", _cultura); break;
                    case int i: s = i.ToString(_cultura); break;
                    default: s = v.ToString(); break;
                }
                bool comillas = s.Contains(_sep) || s.Contains("\"") || s.Contains("\n") || s.Contains("\r");
                return comillas ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            }

            public override string ToString() => _sb.ToString();
        }
    }
}
