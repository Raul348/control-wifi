using System;
using System.Collections.Generic;

namespace VideoAudio.Core
{
    public enum Formato { Mp3, M4a, Wav }

    /// <summary>Modelo de Whisper: más grande, más preciso y más lento.</summary>
    public sealed class ModeloWhisper
    {
        public string Clave { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Archivo { get; set; } = "";
        public int Megas { get; set; }

        public string Url => "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/" + Archivo;
        public override string ToString() => Nombre + " (" + Megas + " MB)";

        public static readonly IReadOnlyList<ModeloWhisper> Todos = new[]
        {
            new ModeloWhisper { Clave = "base", Nombre = "Rápido", Archivo = "ggml-base.bin", Megas = 142 },
            new ModeloWhisper { Clave = "small", Nombre = "Bueno", Archivo = "ggml-small.bin", Megas = 466 },
            new ModeloWhisper { Clave = "medium", Nombre = "Muy bueno (lento)", Archivo = "ggml-medium.bin", Megas = 1462 },
        };
    }

    /// <summary>Idioma de la transcripción.</summary>
    public sealed class Idioma
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public override string ToString() => Nombre;

        public static readonly IReadOnlyList<Idioma> Todos = new[]
        {
            new Idioma { Codigo = "es", Nombre = "Español" },
            new Idioma { Codigo = "en", Nombre = "Inglés" },
            new Idioma { Codigo = "pt", Nombre = "Portugués" },
            new Idioma { Codigo = "auto", Nombre = "Detectar automáticamente" },
        };
    }

    public static class Formatos
    {
        public static string Extension(Formato f)
        {
            switch (f)
            {
                case Formato.M4a: return ".m4a";
                case Formato.Wav: return ".wav";
                default: return ".mp3";
            }
        }

        public static string Nombre(Formato f)
        {
            switch (f)
            {
                case Formato.M4a: return "M4A (AAC)";
                case Formato.Wav: return "WAV (sin compresión)";
                default: return "MP3";
            }
        }

        /// <summary>WAV no usa tasa de bits.</summary>
        public static bool UsaCalidad(Formato f) => f != Formato.Wav;

        public static readonly int[] Calidades = { 128, 192, 256, 320 };
    }
}
