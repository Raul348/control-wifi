using System;
using System.Collections;
using System.Collections.Generic;
using Tekla.Structures;
using Tekla.Structures.Catalogs;
using Tekla.Structures.Model;
using TeklaPiezas.Core;
using UI = Tekla.Structures.Model.UI;

namespace TeklaPiezas
{
    /// <summary>
    /// Todo lo que el programa hace con Tekla pasa por esta clase.
    /// La Open API se conecta al Tekla Structures que está abierto en el mismo equipo.
    /// </summary>
    internal sealed class TeklaLector
    {
        private readonly Model _modelo = new Model();

        /// <summary>Verdadero si Tekla está abierto con un modelo cargado.</summary>
        public bool Conectado => _modelo.GetConnectionStatus();

        /// <summary>Nombre y carpeta del modelo abierto.</summary>
        public string NombreModelo
        {
            get
            {
                ModelInfo info = _modelo.GetInfo();
                return info == null ? "" : info.ModelName;
            }
        }

        public string CarpetaModelo => _modelo.GetInfo()?.ModelPath ?? "";

        /// <summary>Lee todas las piezas del modelo.</summary>
        public List<Pieza> LeerTodo(IProgress<int> progreso = null)
        {
            // AutoFetch trae los datos de los objetos en bloque: mucho más rápido en modelos grandes.
            ModelObjectEnumerator.AutoFetch = true;
            try
            {
                ModelObjectEnumerator objetos = _modelo.GetModelObjectSelector().GetAllObjects();
                return Convertir(objetos, progreso);
            }
            finally
            {
                ModelObjectEnumerator.AutoFetch = false;
            }
        }

        /// <summary>Lee solo las piezas seleccionadas en Tekla.</summary>
        public List<Pieza> LeerSeleccion(IProgress<int> progreso = null)
        {
            ModelObjectEnumerator objetos = new UI.ModelObjectSelector().GetSelectedObjects();
            return Convertir(objetos, progreso);
        }

        private static List<Pieza> Convertir(ModelObjectEnumerator objetos, IProgress<int> progreso)
        {
            var piezas = new List<Pieza>();
            int total = Math.Max(1, objetos.GetSize());
            int i = 0;
            while (objetos.MoveNext())
            {
                i++;
                if (progreso != null && i % 50 == 0) progreso.Report(i * 100 / total);

                // En el modelo hay de todo: pernos, soldaduras, cortes, ejes...
                // Solo interesan las piezas (vigas, columnas, placas, polivigas).
                if (objetos.Current is Part parte)
                    piezas.Add(Leer(parte));
            }
            progreso?.Report(100);
            return piezas;
        }

        /// <summary>Copia los datos de una pieza de Tekla.</summary>
        private static Pieza Leer(Part p)
        {
            var pieza = new Pieza
            {
                Id = p.Identifier.ID,
                Nombre = p.Name ?? "",
                Perfil = p.Profile?.ProfileString ?? "",
                Material = p.Material?.MaterialString ?? "",
                Clase = p.Class ?? "",
                Acabado = p.Finish ?? "",
                PrefijoPieza = p.PartNumber?.Prefix ?? "",
                PrefijoConjunto = p.AssemblyNumber?.Prefix ?? "",
            };

            // Los "report properties" son los mismos atributos que se usan en los informes y planos.
            // Vienen en unidades internas de Tekla: milímetros, kilogramos y mm².
            pieza.LongitudMm = Numero(p, "LENGTH");
            pieza.PesoKg = Numero(p, "WEIGHT");
            pieza.AreaM2 = Numero(p, "AREA") / 1e6;
            pieza.TipoPerfil = Texto(p, "PROFILE_TYPE");
            pieza.TipoMaterial = Texto(p, "MATERIAL_TYPE");
            pieza.MarcaPieza = Texto(p, "PART_POS");
            pieza.MarcaConjunto = Texto(p, "ASSEMBLY_POS");

            if (p.GetPhase(out Phase fase)) pieza.Fase = fase.PhaseNumber;
            return pieza;
        }

        private static double Numero(Part p, string propiedad)
        {
            double valor = 0;
            return p.GetReportProperty(propiedad, ref valor) ? valor : 0;
        }

        private static string Texto(Part p, string propiedad)
        {
            string valor = "";
            return p.GetReportProperty(propiedad, ref valor) ? (valor ?? "").Trim() : "";
        }

        /// <summary>Selecciona en Tekla las piezas indicadas por su ID, para verlas en el modelo.</summary>
        public int Seleccionar(IEnumerable<int> ids)
        {
            var lista = new ArrayList();
            foreach (int id in ids)
            {
                ModelObject o = _modelo.SelectModelObject(new Identifier(id));
                if (o != null) lista.Add(o);
            }
            new UI.ModelObjectSelector().Select(lista);
            return lista.Count;
        }

        // ------------------------------------------------------------------ catálogos

        /// <summary>Nombres de los materiales del catálogo del entorno de Tekla.</summary>
        public static List<string> Materiales()
        {
            var lista = new List<string>();
            var catalogo = new CatalogHandler();
            if (!catalogo.GetConnectionStatus()) return lista;
            MaterialItemEnumerator e = catalogo.GetMaterialItems();
            while (e.MoveNext())
            {
                MaterialItem m = e.Current;
                if (m != null && !string.IsNullOrEmpty(m.MaterialName)) lista.Add(m.MaterialName);
            }
            lista.Sort(StringComparer.OrdinalIgnoreCase);
            return lista;
        }

        /// <summary>Normas y diámetros de pernos del catálogo, por ejemplo "A325N  3/4".</summary>
        public static List<string> Pernos()
        {
            var lista = new List<string>();
            var catalogo = new CatalogHandler();
            if (!catalogo.GetConnectionStatus()) return lista;
            BoltItemEnumerator e = catalogo.GetBoltItems();
            while (e.MoveNext())
            {
                BoltItem b = e.Current;
                if (b != null) lista.Add(b.Standard + "   Ø " + b.Size.ToString("0.##") + " mm");
            }
            return lista;
        }

        /// <summary>Cantidad de perfiles del catálogo (perfiles de biblioteca y paramétricos).</summary>
        public static int CantidadPerfiles()
        {
            var catalogo = new CatalogHandler();
            if (!catalogo.GetConnectionStatus()) return 0;
            int n = 0;
            ProfileItemEnumerator e = catalogo.GetProfileItems();
            while (e.MoveNext()) n++;
            return n;
        }
    }
}
