using System;
using System.Linq;
using PerfilesAisc.Core;
using Xunit;

namespace PerfilesAisc.Tests
{
    public class PruebasNave
    {
        [Fact]
        public void Circulo_por_apoyos_y_clave()
        {
            // Luz 20, flecha 3: R = (9 + 100) / 6 = 18.1667
            var (r, cz) = Nave.Circulo(20, 6, 3);
            Assert.Equal(18.1667, r, 3);
            Assert.Equal(6 + 3 - 18.1667, cz, 3);

            P3 a = Nave.PuntoArco(20, 6, 3, 0);
            P3 m = Nave.PuntoArco(20, 6, 3, 0.5);
            P3 b = Nave.PuntoArco(20, 6, 3, 1);
            Assert.Equal(0, a.X, 6); Assert.Equal(6, a.Z, 6);
            Assert.Equal(10, m.X, 6); Assert.Equal(9, m.Z, 6);
            Assert.Equal(20, b.X, 6); Assert.Equal(6, b.Z, 6);

            // Todos los puntos están sobre el círculo y por encima de los apoyos
            for (double t = 0; t <= 1.0001; t += 0.1)
            {
                P3 q = Nave.PuntoArco(20, 6, 3, t);
                Assert.Equal(r, Math.Sqrt((q.X - 10) * (q.X - 10) + (q.Z - cz) * (q.Z - cz)), 6);
                Assert.True(q.Z >= 6 - 1e-9);
            }
        }

        [Fact]
        public void Nave_curva_con_correas_amarres_y_arriostres()
        {
            var p = new ParametrosNave { Luz = 20, Altura = 6, Flecha = 3, Separacion = 6, Porticos = 4, Tipo = TipoArco.Curvo, Correas = 5 };
            var barras = Nave.Generar(p);
            var r = Nave.Resumen(barras).ToDictionary(x => x.Grupo, x => x);

            Assert.Equal(8, r[GrupoBarra.Columna].Cantidad);
            Assert.Equal(4, r[GrupoBarra.Arco].Cantidad);
            Assert.True(barras.Where(b => b.Grupo == GrupoBarra.Arco).All(b => b.Curva));
            Assert.Equal(5 * 3, r[GrupoBarra.Correa].Cantidad);          // 5 correas × 3 vanos
            Assert.Equal(2 * 3, r[GrupoBarra.Amarre].Cantidad);          // 2 lados × 3 vanos
            Assert.Equal(2 * (4 + 2 * 6), r[GrupoBarra.Arriostre].Cantidad); // 2 vanos extremos × (4 pared + 2×6 cubierta)
            Assert.Equal(8 * 6, r[GrupoBarra.Columna].Longitud, 6);
            Assert.Equal(15 * 6, r[GrupoBarra.Correa].Longitud, 6);

            // Longitud del arco: 2 R asin(L / 2R)
            double arco = 2 * 18.1667 * Math.Asin(20 / (2 * 18.1667));
            Assert.Equal(arco, barras.First(b => b.Grupo == GrupoBarra.Arco).Longitud, 2);
        }

        [Fact]
        public void Nave_por_tramos_sigue_el_arco()
        {
            var p = new ParametrosNave { Luz = 20, Altura = 6, Flecha = 3, Porticos = 2, Tipo = TipoArco.Tramos, Tramos = 8, Correas = 0, Arriostres = false, VigaAmarre = false };
            var barras = Nave.Generar(p);
            var arcos = barras.Where(b => b.Grupo == GrupoBarra.Arco).ToList();
            Assert.Equal(16, arcos.Count);
            Assert.All(arcos, b => Assert.False(b.Curva));
            // Los tramos se encadenan de apoyo a apoyo
            var primero = arcos.Take(8).ToList();
            Assert.Equal(0, primero[0].A.X, 6);
            Assert.Equal(20, primero[7].B.X, 6);
            for (int k = 0; k < 7; k++) Assert.Equal(0, primero[k].B.Distancia(primero[k + 1].A), 9);
        }

        [Fact]
        public void Celosia_tiene_cordones_montantes_y_diagonales()
        {
            var p = new ParametrosNave { Luz = 24, Altura = 7, Flecha = 4, Porticos = 2, Tipo = TipoArco.Celosia, Tramos = 10, Canto = 1.2, Correas = 0, Arriostres = false, VigaAmarre = false };
            var barras = Nave.Generar(p);
            var r = Nave.Resumen(barras).ToDictionary(x => x.Grupo, x => x);
            Assert.Equal(20, r[GrupoBarra.CordonSuperior].Cantidad);
            Assert.Equal(20, r[GrupoBarra.CordonInferior].Cantidad);
            Assert.Equal(22, r[GrupoBarra.Montante].Cantidad);
            Assert.Equal(20, r[GrupoBarra.Diagonal].Cantidad);
            // Cada montante es vertical y mide el canto
            foreach (Barra m in barras.Where(b => b.Grupo == GrupoBarra.Montante))
            {
                Assert.Equal(m.A.X, m.B.X, 6);
                Assert.Equal(1.2, m.Longitud, 6);
            }
        }

        [Fact]
        public void Validacion()
        {
            Assert.Null(new ParametrosNave().Validar());
            Assert.NotNull(new ParametrosNave { Flecha = 10, Luz = 20 }.Validar());
            Assert.NotNull(new ParametrosNave { Porticos = 1 }.Validar());
            Assert.Throws<ArgumentException>(() => Nave.Generar(new ParametrosNave { Luz = 0 }));
        }
    }
}
