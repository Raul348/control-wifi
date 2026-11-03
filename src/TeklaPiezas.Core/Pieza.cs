namespace TeklaPiezas.Core
{
    /// <summary>
    /// Datos de una pieza del modelo de Tekla (viga, columna, placa...).
    /// Es una copia "plana" de lo que se lee con la Open API, para poder ordenarla,
    /// resumirla y exportarla sin depender de Tekla.
    /// </summary>
    public sealed class Pieza
    {
        /// <summary>Identificador interno de Tekla. Sirve para volver a seleccionar la pieza en el modelo.</summary>
        public int Id { get; set; }

        public string Nombre { get; set; } = "";
        public string Perfil { get; set; } = "";
        public string Material { get; set; } = "";
        /// <summary>STEEL, CONCRETE, TIMBER... según Tekla.</summary>
        public string TipoMaterial { get; set; } = "";
        /// <summary>I, L, U, M (tubo rectangular), RO (tubo redondo), B (placa)...</summary>
        public string TipoPerfil { get; set; } = "";
        public string Clase { get; set; } = "";
        public string Acabado { get; set; } = "";
        public string PrefijoPieza { get; set; } = "";
        public string PrefijoConjunto { get; set; } = "";
        /// <summary>Marca de pieza después de numerar, por ejemplo "p12". Vacía si no se ha numerado.</summary>
        public string MarcaPieza { get; set; } = "";
        /// <summary>Marca de conjunto después de numerar, por ejemplo "B3".</summary>
        public string MarcaConjunto { get; set; } = "";
        public int Fase { get; set; }

        /// <summary>Longitud en milímetros.</summary>
        public double LongitudMm { get; set; }
        /// <summary>Peso en kilogramos.</summary>
        public double PesoKg { get; set; }
        /// <summary>Área de superficie (para pintura) en metros cuadrados.</summary>
        public double AreaM2 { get; set; }

        public bool EsAcero => TipoMaterial.ToUpperInvariant() == "STEEL";
        public bool EsPlaca => TipoPerfil.ToUpperInvariant() == "B";
    }
}
