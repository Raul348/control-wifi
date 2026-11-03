using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TeklaPiezas.Core;
using Xunit;

namespace TeklaPiezas.Tests
{
    public class PruebasCore
    {
        private static Pieza P(int id, string perfil, string tipo, double largo, double peso,
            string prefijo = "p", string conjunto = "B", string material = "A36", string tipoMat = "STEEL", string nombre = "VIGA")
            => new Pieza
            {
                Id = id, Perfil = perfil, TipoPerfil = tipo, LongitudMm = largo, PesoKg = peso, AreaM2 = largo / 1000.0,
                PrefijoPieza = prefijo, PrefijoConjunto = conjunto, Material = material, TipoMaterial = tipoMat, Nombre = nombre
            };

        [Fact]
        public void Resumen_agrupa_por_perfil_y_material()
        {
            var piezas = new List<Pieza>
            {
                P(1, "W8X31", "I", 6000, 276.7),
                P(2, "W8X31", "I", 4000, 184.5),
                P(3, "W8X31", "I", 3000, 138.4, material: "A572-GR.50"),
                P(4, "L2X2X1/4", "L", 1500, 4.7),
            };

            List<FilaResumen> r = Resumen.PorPerfil(piezas);

            Assert.Equal(3, r.Count);
            FilaResumen w = r.Single(f => f.Perfil == "W8X31" && f.Material == "A36");
            Assert.Equal(2, w.Cantidad);
            Assert.Equal(10.0, w.LongitudTotalM, 6);
            Assert.Equal(461.2, w.PesoTotalKg, 6);
            Assert.Equal(10.0, w.AreaTotalM2, 6);
            // Ordenado por tipo de perfil: primero I, luego L.
            Assert.Equal("I", r[0].TipoPerfil);
            Assert.Equal("L", r[2].TipoPerfil);
            Assert.Equal(604.3, Resumen.PesoTotal(piezas), 6);
        }

        [Fact]
        public void Revision_encuentra_piezas_sin_prefijo_y_peso_cero()
        {
            var piezas = new List<Pieza>
            {
                P(1, "W8X31", "I", 6000, 276.7),
                P(2, "W8X31", "I", 6000, 276.7, prefijo: ""),
                P(3, "XYZ", "", 1000, 0, conjunto: "", nombre: ""),
            };

            List<Observacion> obs = Revision.Revisar(piezas);

            Assert.Contains(obs, o => o.Pieza.Id == 2 && o.Problema == "Sin prefijo de pieza");
            Assert.Contains(obs, o => o.Pieza.Id == 3 && o.Problema == "Sin prefijo de conjunto");
            Assert.Contains(obs, o => o.Pieza.Id == 3 && o.Problema == "Sin nombre");
            Assert.Contains(obs, o => o.Pieza.Id == 3 && o.Problema.StartsWith("Peso cero"));
            Assert.DoesNotContain(obs, o => o.Pieza.Id == 1);
        }

        [Fact]
        public void Revision_detecta_prefijo_usado_en_tipos_distintos()
        {
            var piezas = new List<Pieza>
            {
                P(1, "W8X31", "I", 6000, 276.7, prefijo: "p"),
                P(2, "L2X2X1/4", "L", 1500, 4.7, prefijo: "p"),
                P(3, "PL1/2X200", "B", 300, 6.0, prefijo: "pl"),
            };

            List<Observacion> obs = Revision.Revisar(piezas);

            Assert.Equal(2, obs.Count(o => o.Problema.Contains("tipos distintos")));
            Assert.DoesNotContain(obs, o => o.Pieza.Id == 3);
        }

        [Fact]
        public void Revision_compara_materiales_con_el_catalogo()
        {
            var piezas = new List<Pieza> { P(1, "W8X31", "I", 6000, 276.7, material: "a36"), P(2, "W8X31", "I", 6000, 276.7, material: "ACERO_RARO") };

            List<Observacion> obs = Revision.Revisar(piezas, new[] { "A36", "A572-GR.50" });

            Observacion o = Assert.Single(obs);
            Assert.Equal(2, o.Pieza.Id);
            Assert.Contains("ACERO_RARO", o.Problema);
        }

        [Fact]
        public void Csv_usa_punto_y_coma_cuando_el_decimal_es_coma()
        {
            var es = new CultureInfo("es-ES");
            string csv = Csv.Resumen(new[] { new FilaResumen { Perfil = "W8X31", TipoPerfil = "I", Material = "A36", Cantidad = 2, LongitudTotalM = 10.5, PesoTotalKg = 461.25 } }, es);
            string[] lineas = csv.Split(new[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(";", Csv.Separador(es));
            Assert.StartsWith("Perfil;Tipo de perfil;Material;Cantidad", lineas[0]);
            Assert.Equal("W8X31;Viga I / H;A36;2;10,5;461,2;0", lineas[1]);
        }

        [Fact]
        public void Csv_pone_comillas_cuando_el_texto_lleva_el_separador()
        {
            var us = new CultureInfo("en-US");
            var p = P(7, "PL1/2X200", "B", 300, 6, nombre: "PLACA, BASE \"A\"");
            string csv = Csv.Piezas(new[] { p }, us);

            Assert.Equal(",", Csv.Separador(us));
            Assert.Contains("7,\"PLACA, BASE \"\"A\"\"\",PL1/2X200,Placa,", csv);
        }

        [Theory]
        [InlineData("I", "Viga I / H")]
        [InlineData("l", "Ángulo")]
        [InlineData("M", "Tubo rectangular")]
        [InlineData("RO", "Tubo redondo")]
        [InlineData("B", "Placa")]
        [InlineData("XX", "XX")]
        public void Nombres_de_tipo_de_perfil(string tipo, string nombre)
        {
            Assert.Equal(nombre, Revision.NombreTipoPerfil(tipo));
        }
    }
}
