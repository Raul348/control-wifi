using System;
using System.Collections.Generic;

namespace PerfilesAisc.Core
{
    /// <summary>Punto en metros, en coordenadas locales de la nave: X a lo ancho (luz), Y a lo largo, Z hacia arriba.</summary>
    public struct P3
    {
        public double X, Y, Z;
        public P3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Distancia(P3 o) => Math.Sqrt((X - o.X) * (X - o.X) + (Y - o.Y) * (Y - o.Y) + (Z - o.Z) * (Z - o.Z));
        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
    }

    public enum TipoArco { Curvo, Tramos, Celosia }

    /// <summary>Qué es cada barra dentro de la nave. Decide el perfil y cómo se crea en Revit.</summary>
    public enum GrupoBarra { Columna, Arco, CordonSuperior, CordonInferior, Montante, Diagonal, Correa, Amarre, Arriostre }

    /// <summary>Una barra de la nave: recta entre A y B, o arco por A, Medio y B.</summary>
    public sealed class Barra
    {
        public GrupoBarra Grupo;
        public P3 A, B, Medio;
        public bool Curva;
        public double Longitud => Curva ? LongitudArco() : A.Distancia(B);

        private double LongitudArco()
        {
            // Longitud de la cuerda y la flecha → radio → longitud del arco.
            double cuerda = A.Distancia(B);
            var m = new P3((A.X + B.X) / 2, (A.Y + B.Y) / 2, (A.Z + B.Z) / 2);
            double flecha = Medio.Distancia(m);
            if (flecha < 1e-9) return cuerda;
            double r = (flecha * flecha + cuerda * cuerda / 4) / (2 * flecha);
            return 2 * r * Math.Asin(Math.Min(1, cuerda / (2 * r)));
        }
    }

    /// <summary>Datos de la nave, en metros.</summary>
    public sealed class ParametrosNave
    {
        public double Luz = 20;                 // ancho entre ejes de columnas
        public double Altura = 6;               // altura de las columnas
        public double Flecha = 3;               // altura del arco sobre el apoyo
        public double Separacion = 6;           // distancia entre pórticos
        public int Porticos = 5;
        public TipoArco Tipo = TipoArco.Curvo;
        public int Tramos = 8;                  // tramos rectos o paneles de la celosía
        public double Canto = 1.0;              // canto de la celosía
        public int Correas = 6;                 // correas por arco, sin contar los extremos
        public bool VigaAmarre = true;          // viga entre las cabezas de las columnas, a lo largo
        public bool Arriostres = true;          // cruces de San Andrés en los vanos extremos

        public string Validar()
        {
            if (Luz <= 0) return "La luz debe ser mayor que cero.";
            if (Altura <= 0) return "La altura de columnas debe ser mayor que cero.";
            if (Flecha <= 0) return "La flecha del arco debe ser mayor que cero.";
            if (Flecha >= Luz / 2) return "La flecha debe ser menor que la mitad de la luz (medio círculo como máximo).";
            if (Porticos < 2) return "Se necesitan al menos 2 pórticos.";
            if (Separacion <= 0) return "La separación entre pórticos debe ser mayor que cero.";
            if (Tramos < 2) return "Se necesitan al menos 2 tramos.";
            if (Tipo == TipoArco.Celosia && (Canto <= 0 || Canto >= Flecha + Altura)) return "El canto de la celosía no es válido.";
            if (Correas < 0) return "El número de correas no puede ser negativo.";
            return null;
        }
    }

    /// <summary>Geometría de una nave industrial con cubierta en arco.</summary>
    public static class Nave
    {
        /// <summary>Radio y altura del centro del círculo que pasa por los apoyos (altura H) y la clave (H + f).</summary>
        public static (double Radio, double CentroZ) Circulo(double luz, double altura, double flecha)
        {
            double r = (flecha * flecha + luz * luz / 4) / (2 * flecha);
            return (r, altura + flecha - r);
        }

        /// <summary>Punto del arco para t entre 0 (apoyo izquierdo) y 1 (apoyo derecho), a igual ángulo.</summary>
        public static P3 PuntoArco(double luz, double altura, double flecha, double t, double y = 0)
        {
            var (r, cz) = Circulo(luz, altura, flecha);
            double cx = luz / 2;
            double a0 = Math.Atan2(altura - cz, 0 - cx);        // ángulo del apoyo izquierdo
            double a1 = Math.Atan2(altura - cz, luz - cx);      // ángulo del apoyo derecho
            if (a1 > a0) a1 -= 2 * Math.PI;                     // recorrer por arriba (sentido horario)
            double a = a0 + (a1 - a0) * t;
            return new P3(cx + r * Math.Cos(a), y, cz + r * Math.Sin(a));
        }

        public static List<Barra> Generar(ParametrosNave p)
        {
            string error = p.Validar();
            if (error != null) throw new ArgumentException(error);

            var barras = new List<Barra>();
            double[] ys = new double[p.Porticos];
            for (int i = 0; i < p.Porticos; i++) ys[i] = i * p.Separacion;

            // Puntos de las correas sobre el arco (t de 0 a 1, incluidos los apoyos).
            var ts = new List<double>();
            for (int k = 0; k <= p.Correas + 1; k++) ts.Add((double)k / (p.Correas + 1));

            foreach (double y in ys)
            {
                // Columnas
                barras.Add(new Barra { Grupo = GrupoBarra.Columna, A = new P3(0, y, 0), B = new P3(0, y, p.Altura) });
                barras.Add(new Barra { Grupo = GrupoBarra.Columna, A = new P3(p.Luz, y, 0), B = new P3(p.Luz, y, p.Altura) });

                // Arco
                switch (p.Tipo)
                {
                    case TipoArco.Curvo:
                        barras.Add(Arco(GrupoBarra.Arco, p.Luz, p.Altura, p.Flecha, y));
                        break;
                    case TipoArco.Tramos:
                        for (int k = 0; k < p.Tramos; k++)
                            barras.Add(new Barra
                            {
                                Grupo = GrupoBarra.Arco,
                                A = PuntoArco(p.Luz, p.Altura, p.Flecha, (double)k / p.Tramos, y),
                                B = PuntoArco(p.Luz, p.Altura, p.Flecha, (double)(k + 1) / p.Tramos, y)
                            });
                        break;
                    default:
                        Celosia(barras, p, y);
                        break;
                }
            }

            // Correas y vigas de amarre entre pórticos consecutivos
            for (int i = 0; i + 1 < p.Porticos; i++)
            {
                foreach (double t in ts)
                {
                    bool extremo = t <= 0 || t >= 1;
                    if (extremo && !p.VigaAmarre) continue;
                    barras.Add(new Barra
                    {
                        Grupo = extremo ? GrupoBarra.Amarre : GrupoBarra.Correa,
                        A = PuntoArco(p.Luz, p.Altura, p.Flecha, t, ys[i]),
                        B = PuntoArco(p.Luz, p.Altura, p.Flecha, t, ys[i + 1])
                    });
                }
            }

            // Arriostres en cruz en el primer y el último vano: en las dos paredes y en la cubierta
            if (p.Arriostres && p.Porticos >= 2)
            {
                var vanos = new List<int> { 0 };
                if (p.Porticos > 2) vanos.Add(p.Porticos - 2);
                foreach (int i in vanos)
                {
                    double y0 = ys[i], y1 = ys[i + 1];
                    foreach (double x in new[] { 0.0, p.Luz })
                    {
                        barras.Add(new Barra { Grupo = GrupoBarra.Arriostre, A = new P3(x, y0, 0), B = new P3(x, y1, p.Altura) });
                        barras.Add(new Barra { Grupo = GrupoBarra.Arriostre, A = new P3(x, y1, 0), B = new P3(x, y0, p.Altura) });
                    }
                    for (int k = 0; k + 1 < ts.Count; k++)
                    {
                        barras.Add(new Barra
                        {
                            Grupo = GrupoBarra.Arriostre,
                            A = PuntoArco(p.Luz, p.Altura, p.Flecha, ts[k], y0),
                            B = PuntoArco(p.Luz, p.Altura, p.Flecha, ts[k + 1], y1)
                        });
                        barras.Add(new Barra
                        {
                            Grupo = GrupoBarra.Arriostre,
                            A = PuntoArco(p.Luz, p.Altura, p.Flecha, ts[k + 1], y0),
                            B = PuntoArco(p.Luz, p.Altura, p.Flecha, ts[k], y1)
                        });
                    }
                }
            }
            return barras;
        }

        private static Barra Arco(GrupoBarra grupo, double luz, double altura, double flecha, double y)
            => new Barra
            {
                Grupo = grupo, Curva = true,
                A = new P3(0, y, altura), B = new P3(luz, y, altura), Medio = new P3(luz / 2, y, altura + flecha)
            };

        /// <summary>Celosía en arco: cordón superior, cordón inferior paralelo (mismo radio, bajado el canto), montantes y diagonales en W.</summary>
        private static void Celosia(List<Barra> barras, ParametrosNave p, double y)
        {
            double d = p.Canto;
            // Cordones: curvos si el arco es "curvo", si no por tramos. Aquí siempre por tramos entre nudos, que es como se fabrica.
            var sup = new List<P3>();
            var inf = new List<P3>();
            for (int k = 0; k <= p.Tramos; k++)
            {
                double t = (double)k / p.Tramos;
                sup.Add(PuntoArco(p.Luz, p.Altura, p.Flecha, t, y));
                P3 q = PuntoArco(p.Luz, p.Altura - d, p.Flecha, t, y);   // mismo círculo bajado el canto
                inf.Add(q);
            }
            for (int k = 0; k < p.Tramos; k++)
            {
                barras.Add(new Barra { Grupo = GrupoBarra.CordonSuperior, A = sup[k], B = sup[k + 1] });
                barras.Add(new Barra { Grupo = GrupoBarra.CordonInferior, A = inf[k], B = inf[k + 1] });
            }
            for (int k = 0; k <= p.Tramos; k++)
                barras.Add(new Barra { Grupo = GrupoBarra.Montante, A = inf[k], B = sup[k] });
            // Diagonales: suben hacia el centro desde cada apoyo (simétricas)
            for (int k = 0; k < p.Tramos; k++)
            {
                bool izquierda = k < p.Tramos / 2.0;
                barras.Add(izquierda
                    ? new Barra { Grupo = GrupoBarra.Diagonal, A = inf[k], B = sup[k + 1] }
                    : new Barra { Grupo = GrupoBarra.Diagonal, A = sup[k], B = inf[k + 1] });
            }
        }

        /// <summary>Resumen por grupo: cantidad y longitud total en metros.</summary>
        public static List<(GrupoBarra Grupo, int Cantidad, double Longitud)> Resumen(IEnumerable<Barra> barras)
        {
            var dic = new Dictionary<GrupoBarra, (int, double)>();
            foreach (Barra b in barras)
            {
                dic.TryGetValue(b.Grupo, out var v);
                dic[b.Grupo] = (v.Item1 + 1, v.Item2 + b.Longitud);
            }
            var lista = new List<(GrupoBarra, int, double)>();
            foreach (GrupoBarra g in Enum.GetValues(typeof(GrupoBarra)))
                if (dic.TryGetValue(g, out var v)) lista.Add((g, v.Item1, v.Item2));
            return lista;
        }

        public static string NombreGrupo(GrupoBarra g)
        {
            switch (g)
            {
                case GrupoBarra.Columna: return "Columnas";
                case GrupoBarra.Arco: return "Arcos";
                case GrupoBarra.CordonSuperior: return "Cordones superiores";
                case GrupoBarra.CordonInferior: return "Cordones inferiores";
                case GrupoBarra.Montante: return "Montantes";
                case GrupoBarra.Diagonal: return "Diagonales";
                case GrupoBarra.Correa: return "Correas";
                case GrupoBarra.Amarre: return "Vigas de amarre";
                default: return "Arriostres";
            }
        }
    }
}
