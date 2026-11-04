using System.Linq;
using PerfilesAisc.Core;
using Xunit;

namespace PerfilesAisc.Tests
{
    public class PruebasCatalogo
    {
        private static readonly Catalogo C = Catalogo.Instancia;

        [Fact]
        public void Carga_todos_los_perfiles()
        {
            Assert.Equal(1575, C.Perfiles.Count);
            Assert.Equal(331, C.Perfiles.Count(p => p.Forma == Forma.I));
            Assert.Equal(367, C.Perfiles.Count(p => p.Forma == Forma.TuboRectangular));
            Assert.Equal(165, C.Perfiles.Count(p => p.Forma == Forma.TuboRedondo));
        }

        [Fact]
        public void W6X8_5_tiene_las_medidas_de_Tekla()
        {
            Perfil p = C.Buscar("W6X8.5");
            Assert.NotNull(p);
            Assert.Equal(Forma.I, p.Forma);
            Assert.Equal("W", p.Serie);
            Assert.Equal(148.08, Unidades.Mm(p.AltoIn.Value), 2);
            Assert.Equal(100.08, Unidades.Mm(p.AnchoIn.Value), 2);
            Assert.Equal(4.32, Unidades.Mm(p.AlmaIn.Value), 2);
            Assert.Equal(4.95, Unidades.Mm(p.AlaIn.Value), 2);
            Assert.Equal(12.65, p.PesoKgM, 1);
            Assert.Equal(16.26, Unidades.Cm2(p.AreaIn2.Value), 1);
        }

        [Fact]
        public void Arbol_ordenado_como_Tekla()
        {
            Nodo perfilesI = C.Arbol[0];
            Assert.Equal("Perfiles I", perfilesI.Texto);
            Assert.Equal(new[] { "HP", "W", "M", "S" }, perfilesI.Hijos.Select(h => h.Texto));

            Nodo w = perfilesI.Hijos[1];
            Assert.Equal("W4", w.Hijos[0].Texto);
            Assert.Equal("W44", w.Hijos.Last().Texto);
            Nodo w6 = w.Hijos.Single(h => h.Texto == "W6");
            Assert.Equal(new[] { "W6X8.5", "W6X9", "W6X12", "W6X15", "W6X16", "W6X20", "W6X25" }, w6.Hijos.Select(h => h.Texto));

            Nodo angulos = C.Arbol.Single(n => n.Texto == "Ángulos");
            Nodo l = angulos.Hijos.Single();
            Assert.Equal(new[] { "L2", "L2-1/2", "L3", "L3-1/2", "L4", "L5", "L6", "L7", "L8" }, l.Hijos.Select(h => h.Texto));

            Nodo redondos = C.Arbol.Single(n => n.Texto == "Tubos redondos");
            Assert.Equal(new[] { "HSS", "Pipe" }, redondos.Hijos.Select(h => h.Texto));
            Assert.All(redondos.Hijos[1].Hijos, h => Assert.True(h.EsPerfil));
        }

        [Theory]
        [InlineData("W6X8.5", "W", "W6", 6)]
        [InlineData("HP8X36", "HP", "HP8", 8)]
        [InlineData("L2-1/2X2X1/4", "L", "L2-1/2", 2.5)]
        [InlineData("2L4X4X1/2", "2L", "2L4", 4)]
        [InlineData("HSS20X12X5/8", "HSS", "HSS20", 20)]
        [InlineData("HSS1-1/2X1-1/2X1/4", "HSS", "HSS1-1/2", 1.5)]
        [InlineData("Pipe3/4STD", "Pipe", "Pipe", 0.75)]
        [InlineData("WT2.5X9.5", "WT", "WT2.5", 2.5)]
        public void Clasificacion_por_nombre(string nombre, string serie, string tamano, double numero)
        {
            Assert.Equal(serie, Perfil.SerieDe(nombre));
            Assert.Equal(tamano, Perfil.TamanoDe(nombre));
            Assert.Equal(numero, Perfil.NumeroDe(nombre), 6);
        }

        [Fact]
        public void Filtro_con_comodines_y_texto()
        {
            Assert.Equal(7, C.Filtrar("W6X*").Count);
            Assert.Contains(C.Filtrar("8.5"), p => p.Nombre == "W6X8.5");
            Assert.Equal(C.Perfiles.Count, C.Filtrar("*").Count);
            Assert.Empty(C.Filtrar("ZZZ"));

            var arbol = C.ArbolFiltrado("W6X*");
            Nodo unico = Assert.Single(arbol);
            Assert.Equal("Perfiles I", unico.Texto);
            Assert.Equal(7, unico.Perfiles().Count());
        }

        [Fact]
        public void Nombres_con_x_minuscula_se_normalizan()
        {
            Assert.NotNull(C.Buscar("W18X192"));
            Assert.DoesNotContain(C.Perfiles, p => p.Nombre.Contains("x"));
        }

        [Fact]
        public void Radio_del_acuerdo()
        {
            Perfil p = C.Buscar("W6X8.5");
            // k(des) = 0.445 in, tf = 0.195 in → r = 0.25 in = 6.35 mm
            Assert.Equal(6.35, Unidades.Mm(p.RadioIn.Value), 2);
            Assert.Null(C.Buscar("HSS6X6X1/4").RadioIn);
        }

        [Fact]
        public void Pipes_y_tubos_redondos()
        {
            Perfil pipe = C.Buscar("Pipe1STD");
            Assert.Equal(Forma.TuboRedondo, pipe.Forma);
            Assert.Equal(33.4, Unidades.Mm(pipe.AltoIn.Value), 1);
            Assert.Equal(3.15, Unidades.Mm(pipe.AlmaIn.Value), 2);   // t(des) = 0.124 in
        }
    }
}
