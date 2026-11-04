using System;

namespace PerfilesAisc.Core
{
    /// <summary>Conversión de las unidades del catálogo AISC (pulgadas y libras) al sistema métrico.</summary>
    public static class Unidades
    {
        public const double InAMm = 25.4;
        public const double In2ACm2 = 6.4516;
        public const double In3ACm3 = 16.387064;
        public const double In4ACm4 = 41.6231426;
        public const double In6ACm6 = 268.7336;          // 2.54^6
        public const double LbFtAKgM = 1.48816394;
        public const double Ft2FtAM2M = 0.3048;          // ft²/ft → m²/m

        // Nombres cortos usados en el resto del programa.
        public static double Mm(double pulgadas) => pulgadas * InAMm;
        public static double Cm2(double in2) => in2 * In2ACm2;
        public static double Cm3(double in3) => in3 * In3ACm3;
        public static double Cm4(double in4) => in4 * In4ACm4;
        public static double Cm6(double in6) => in6 * In6ACm6;
        public static double Cm(double pulgadas) => pulgadas * 2.54;
        public static double KgM(double lbft) => lbft * LbFtAKgM;
        public static double M2M(double ft2ft) => ft2ft * Ft2FtAM2M;

        /// <summary>Texto con los decimales adecuados al tamaño del número.</summary>
        public static string Texto(double v)
        {
            double a = Math.Abs(v);
            string f = a >= 1000 ? "N0" : a >= 100 ? "N1" : a >= 10 ? "N2" : "N3";
            return v.ToString(f);
        }
    }
}
