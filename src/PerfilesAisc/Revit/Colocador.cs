using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.Structure.StructuralSections;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using PerfilesAisc.Core;

namespace PerfilesAisc.Revit
{
    /// <summary>
    /// Coloca vigas y columnas con un perfil del catálogo.
    /// 1. Busca en el proyecto un tipo con ese nombre en una familia de la misma forma (I, C, L, HSS...).
    /// 2. Si no existe, busca una familia cargada de esa forma; si no hay, la carga de la biblioteca de Revit.
    /// 3. Duplica un tipo de esa familia con el nombre del perfil y le pone las medidas del catálogo.
    /// 4. Pide los puntos al usuario y crea las piezas, una por transacción, hasta que pulse Esc.
    /// </summary>
    internal sealed class Colocador
    {
        private const ObjectSnapTypes Snaps = ObjectSnapTypes.Endpoints | ObjectSnapTypes.Midpoints | ObjectSnapTypes.Intersections |
                                              ObjectSnapTypes.Centers | ObjectSnapTypes.Points | ObjectSnapTypes.Nearest;

        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public Colocador(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
        }

        // ================================================================== colocar

        public string ColocarVigas(Perfil perfil)
        {
            FamilySymbol tipo = ObtenerTipo(perfil, columna: false, out string aviso);
            if (tipo == null) return aviso;
            Level nivel = NivelActivo();
            if (nivel == null) return "El proyecto no tiene niveles.";

            int n = 0;
            try
            {
                while (true)
                {
                    XYZ p1 = _uidoc.Selection.PickPoint(Snaps, perfil.Nombre + ": primer punto de la viga (Esc para terminar)");
                    XYZ p2 = _uidoc.Selection.PickPoint(Snaps, perfil.Nombre + ": segundo punto de la viga");
                    if (p1.DistanceTo(p2) < 0.01) continue;
                    using (var t = new Transaction(_doc, "Perfiles AISC - viga " + perfil.Nombre))
                    {
                        t.Start();
                        if (!tipo.IsActive) tipo.Activate();
                        _doc.Create.NewFamilyInstance(Line.CreateBound(p1, p2), tipo, nivel, StructuralType.Beam);
                        t.Commit();
                    }
                    n++;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { /* Esc: fin */ }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
            {
                return "No se pueden elegir puntos en esta vista. Use una vista de planta o de estructura.\n" + ex.Message;
            }
            return Resumen(aviso, n, "vigas", perfil);
        }

        public string ColocarColumnas(Perfil perfil)
        {
            FamilySymbol tipo = ObtenerTipo(perfil, columna: true, out string aviso);
            if (tipo == null) return aviso;
            Level nivel = NivelActivo();
            if (nivel == null) return "El proyecto no tiene niveles.";
            Level superior = NivelSuperior(nivel);

            int n = 0;
            try
            {
                while (true)
                {
                    XYZ p = _uidoc.Selection.PickPoint(Snaps, perfil.Nombre + ": punto de la columna (Esc para terminar)");
                    using (var t = new Transaction(_doc, "Perfiles AISC - columna " + perfil.Nombre))
                    {
                        t.Start();
                        if (!tipo.IsActive) tipo.Activate();
                        FamilyInstance col = _doc.Create.NewFamilyInstance(p, tipo, nivel, StructuralType.Column);
                        // Altura: hasta el nivel siguiente; si no hay, 3 m.
                        if (superior != null)
                            col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(superior.Id);
                        else
                        {
                            col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(nivel.Id);
                            col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(3000 / 304.8);
                        }
                        t.Commit();
                    }
                    n++;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { /* Esc: fin */ }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
            {
                return "No se pueden elegir puntos en esta vista. Use una vista de planta o de estructura.\n" + ex.Message;
            }
            return Resumen(aviso, n, "columnas", perfil);
        }

        private static string Resumen(string aviso, int n, string que, Perfil perfil)
        {
            string texto = n == 0 ? "" : n + " " + que + " " + perfil.Nombre + " colocadas.";
            return string.IsNullOrEmpty(aviso) ? texto : (aviso + "\n\n" + texto).Trim();
        }

        private Level NivelActivo()
        {
            Level nivel = _doc.ActiveView?.GenLevel;
            return nivel ?? Niveles().FirstOrDefault();
        }

        private List<Level> Niveles()
            => new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();

        private Level NivelSuperior(Level nivel)
            => Niveles().FirstOrDefault(l => l.Elevation > nivel.Elevation + 0.01);

        // ================================================================== tipo (FamilySymbol)

        /// <summary>Devuelve el tipo listo para usar, o null con el motivo en <paramref name="aviso"/>.</summary>
        private FamilySymbol ObtenerTipo(Perfil perfil, bool columna, out string aviso)
        {
            aviso = "";
            BuiltInCategory categoria = columna ? BuiltInCategory.OST_StructuralColumns : BuiltInCategory.OST_StructuralFraming;
            List<FamilySymbol> tipos = TiposDe(categoria);

            // 1. Ya existe un tipo con ese nombre en una familia adecuada.
            FamilySymbol existente = tipos
                .Where(s => NombreIgual(s.Name, perfil.Nombre))
                .OrderByDescending(s => Puntaje(s, perfil))
                .FirstOrDefault(s => Puntaje(s, perfil) > 0);
            if (existente != null) return existente;

            // 2. Una familia cargada de la forma correcta.
            FamilySymbol modelo = tipos.OrderByDescending(s => Puntaje(s, perfil)).FirstOrDefault(s => Puntaje(s, perfil) > 0);

            // 3. Cargar la familia de la biblioteca de Revit.
            if (modelo == null)
            {
                Family familia = CargarDeBiblioteca(perfil, columna, out string error);
                if (familia == null)
                {
                    aviso = error;
                    return null;
                }
                tipos = familia.GetFamilySymbolIds().Select(id => _doc.GetElement(id)).OfType<FamilySymbol>().ToList();
                existente = tipos.FirstOrDefault(s => NombreIgual(s.Name, perfil.Nombre));
                if (existente != null) return existente;
                modelo = tipos.FirstOrDefault();
                if (modelo == null)
                {
                    aviso = "La familia " + familia.Name + " no tiene tipos.";
                    return null;
                }
            }

            // 4. Duplicar y poner las medidas.
            using (var t = new Transaction(_doc, "Perfiles AISC - tipo " + perfil.Nombre))
            {
                t.Start();
                FamilySymbol nuevo;
                try { nuevo = (FamilySymbol)modelo.Duplicate(perfil.Nombre); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { nuevo = (FamilySymbol)modelo.Duplicate(perfil.Nombre + " (AISC)"); }
                List<string> faltantes = PonerMedidas(nuevo, perfil);
                t.Commit();

                if (faltantes.Count > 0)
                    aviso = "Se creó el tipo " + nuevo.Name + " en la familia " + modelo.Family.Name + ", pero no se pudieron ajustar estas medidas: "
                            + string.Join(", ", faltantes) + ".\nRevise el tipo en Revit.";
                return nuevo;
            }
        }

        private List<FamilySymbol> TiposDe(BuiltInCategory categoria)
            => new FilteredElementCollector(_doc).OfCategory(categoria).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().ToList();

        /// <summary>"W6X8.5", "W6x8.5" y "W 6X8.5" son el mismo nombre.</summary>
        private static bool NombreIgual(string a, string b)
            => Normalizar(a) == Normalizar(b);

        private static string Normalizar(string s)
            => (s ?? "").Replace(" ", "").Replace('x', 'X').ToUpperInvariant();

        /// <summary>Qué tan adecuada es la familia de un tipo para el perfil: forma de la sección (10) y nombre (5).</summary>
        private static int Puntaje(FamilySymbol s, Perfil perfil)
        {
            StructuralSectionShape forma = StructuralSectionShape.NotDefined;
            try
            {
                StructuralSection sec = s.GetStructuralSection();
                if (sec != null) forma = sec.StructuralSectionShape;
            }
            catch { /* familias sin sección definida */ }

            int p = 0;
            if (FormaCoincide(forma, perfil.Forma)) p += 10;
            bool nombre = NombreFamiliaCoincide(s.Family?.Name ?? "", perfil);
            if (nombre) p += 5;
            // Una familia sin forma definida pero con nombre claro (por ejemplo "LL-Double Angle") también sirve.
            if (p == 5 && (forma == StructuralSectionShape.NotDefined || forma == StructuralSectionShape.UserDefined)) p = 6;
            else if (p == 5) p = 0;   // nombre parecido pero forma distinta: no sirve
            return p;
        }

        private static bool FormaCoincide(StructuralSectionShape s, Forma f)
        {
            switch (f)
            {
                case Forma.I:
                    return s == StructuralSectionShape.IWideFlange || s == StructuralSectionShape.IParallelFlange
                           || s == StructuralSectionShape.ISlopedFlange || s == StructuralSectionShape.IWelded;
                case Forma.Canal:
                    return s == StructuralSectionShape.CParallelFlange || s == StructuralSectionShape.CSlopedFlange;
                case Forma.T:
                    return s == StructuralSectionShape.StructuralTees || s == StructuralSectionShape.ISplitParallelFlange
                           || s == StructuralSectionShape.ISplitSlopedFlange;
                case Forma.Angulo:
                    return s == StructuralSectionShape.LAngle;
                case Forma.AnguloDoble:
                    return false;   // Revit no tiene forma propia para ángulos dobles: se reconoce por el nombre
                case Forma.TuboRectangular:
                    return s == StructuralSectionShape.RectangleHSS;
                default:
                    return s == StructuralSectionShape.RoundHSS || s == StructuralSectionShape.PipeStandard;
            }
        }

        /// <summary>Palabras que identifican la familia de cada serie, en inglés y en español.</summary>
        private static string[] Palabras(Perfil perfil)
        {
            switch (perfil.Serie)
            {
                case "W": return new[] { "W-", "W ", "WIDE FLANGE", "ALA ANCHA" };
                case "HP": return new[] { "HP" };
                case "M": return new[] { "M-", "MISCELLANEOUS WIDE" };
                case "S": return new[] { "S-", "AMERICAN STANDARD" };
                case "C": return new[] { "C-", "CHANNEL", "CANAL" };
                case "MC": return new[] { "MC" };
                case "WT": return new[] { "WT", "STRUCTURAL TEE", "TEE" };
                case "MT": return new[] { "MT" };
                case "ST": return new[] { "ST-", "ST " };
                case "L": return new[] { "L-", "ANGLE", "ANGULO", "ÁNGULO" };
                case "2L": return new[] { "LL", "2L", "DOUBLE", "DOBLE" };
                case "Pipe": return new[] { "PIPE", "TUBER" };
                default:
                    return perfil.Forma == Forma.TuboRedondo ? new[] { "HSS-ROUND", "ROUND", "REDOND" } : new[] { "HSS" };
            }
        }

        private static bool NombreFamiliaCoincide(string familia, Perfil perfil)
        {
            string f = familia.ToUpperInvariant();
            bool redonda = f.Contains("ROUND") || f.Contains("REDOND") || f.Contains("PIPE") || f.Contains("TUBER");
            if (perfil.Forma == Forma.TuboRectangular && redonda) return false;
            if (perfil.Forma == Forma.TuboRedondo && perfil.Serie != "Pipe" && !redonda) return false;
            if (perfil.Serie == "L" && (f.Contains("LL") || f.Contains("DOUBLE") || f.Contains("DOBLE"))) return false;
            if (perfil.Serie == "W" && (f.StartsWith("WT") || f.StartsWith("WWF"))) return false;
            return Palabras(perfil).Any(p => f.StartsWith(p) || f.Contains(p));
        }

        // ================================================================== biblioteca

        /// <summary>Busca en las bibliotecas de Revit la familia de la serie y la carga en el proyecto.</summary>
        private Family CargarDeBiblioteca(Perfil perfil, bool columna, out string error)
        {
            error = "";
            var carpetas = new List<string>();
            try { carpetas.AddRange(_doc.Application.GetLibraryPaths().Values); } catch { /* sin bibliotecas configuradas */ }
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            carpetas.Add(Path.Combine(programData, "Autodesk", "RVT " + _doc.Application.VersionNumber, "Libraries"));

            string[] columnaPalabras = { "COLUMN", "COLUMNA", "PILAR" };
            foreach (string carpeta in carpetas.Where(Directory.Exists).Distinct())
            {
                IEnumerable<string> archivos;
                try { archivos = Directory.EnumerateFiles(carpeta, "*.rfa", SearchOption.AllDirectories); }
                catch { continue; }

                foreach (string ruta in archivos)
                {
                    string nombre = Path.GetFileNameWithoutExtension(ruta).ToUpperInvariant();
                    bool esColumna = columnaPalabras.Any(nombre.Contains);
                    if (esColumna != columna) continue;
                    if (!NombreFamiliaCoincide(nombre, perfil)) continue;
                    // Debe estar en una carpeta de acero/estructura, no de muebles u otros.
                    string rutaMayus = ruta.ToUpperInvariant();
                    if (!(rutaMayus.Contains("STEEL") || rutaMayus.Contains("ACERO") || rutaMayus.Contains("STRUCTURAL") || rutaMayus.Contains("ESTRUCTUR")))
                        continue;

                    using (var t = new Transaction(_doc, "Perfiles AISC - cargar familia"))
                    {
                        t.Start();
                        bool ok = _doc.LoadFamily(ruta, out Family familia);
                        t.Commit();
                        if (ok && familia != null) return familia;
                        if (familia == null)
                        {
                            // Ya estaba cargada con ese nombre pero no la encontramos por forma: se busca por nombre.
                            string nombreFamilia = Path.GetFileNameWithoutExtension(ruta);
                            Family existente = new FilteredElementCollector(_doc).OfClass(typeof(Family)).Cast<Family>()
                                .FirstOrDefault(f => f.Name.Equals(nombreFamilia, StringComparison.OrdinalIgnoreCase));
                            if (existente != null) return existente;
                        }
                    }
                }
            }

            error = "No hay en el proyecto una familia de " + (columna ? "columna" : "viga") + " para perfiles " + perfil.Serie +
                    " y no se encontró en la biblioteca de Revit.\n\n" +
                    "Cárguela con Insertar → Cargar familia, en la carpeta " +
                    (columna ? "Structural Columns\\Steel" : "Structural Framing\\Steel") +
                    " (por ejemplo " + FamiliaSugerida(perfil, columna) + "), y vuelva a intentarlo.";
            return null;
        }

        private static string FamiliaSugerida(Perfil perfil, bool columna)
        {
            string sufijo = columna ? "-Column.rfa" : ".rfa";
            switch (perfil.Serie)
            {
                case "W": return "W-Wide Flange" + sufijo;
                case "HP": return "HP-Bearing Pile" + sufijo;
                case "M": return "M-Miscellaneous Wide Flange" + sufijo;
                case "S": return "S-American Standard" + sufijo;
                case "C": return "C-Channel" + sufijo;
                case "MC": return "MC-Miscellaneous Channel" + sufijo;
                case "WT": return "WT-Structural Tee" + sufijo;
                case "MT": return "MT-Miscellaneous Tee" + sufijo;
                case "ST": return "ST-Structural Tee" + sufijo;
                case "L": return "L-Angle" + sufijo;
                case "2L": return "LL-Double Angle" + sufijo;
                case "Pipe": return "Pipe-Standard" + sufijo;
                default: return perfil.Forma == Forma.TuboRedondo ? "HSS-Round Hollow Structural Section" + sufijo : "HSS-Hollow Structural Section" + sufijo;
            }
        }

        // ================================================================== medidas del tipo

        /// <summary>Pone las medidas del catálogo en el tipo. Devuelve las que no se pudieron poner.</summary>
        private static List<string> PonerMedidas(FamilySymbol tipo, Perfil p)
        {
            var faltantes = new List<string>();
            void Medida(string etiqueta, double? pulgadas, BuiltInParameter? bip, params string[] nombres)
            {
                if (pulgadas == null) return;
                if (!PonerLongitud(tipo, pulgadas.Value, bip, nombres)) faltantes.Add(etiqueta);
            }

            switch (p.Forma)
            {
                case Forma.I:
                case Forma.T:
                case Forma.Canal:
                    Medida("altura d", p.V("d"), BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT, "d", "h", "H");
                    Medida("ancho bf", p.V("bf"), BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH, "bf", "b", "B");
                    Medida("alma tw", p.V("tw"), BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_WEBTHICKNESS, "tw", "s");
                    Medida("ala tf", p.V("tf"), BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_FLANGETHICKNESS, "tf", "t");
                    PonerLongitud(tipo, p.V("k") ?? 0, null, "k");
                    PonerLongitud(tipo, p.V("k1") ?? 0, null, "k1");
                    if (p.RadioIn != null) PonerLongitud(tipo, p.RadioIn.Value, BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_WEBFILLET, "r", "r1");
                    break;
                case Forma.Angulo:
                case Forma.AnguloDoble:
                    Medida("lado d", p.V("d"), BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT, "d", "a", "h");
                    Medida("lado b", p.V("b"), BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH, "b", "bf");
                    Medida("espesor t", p.V("t"), BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_FLANGETHICKNESS, "t", "tf", "tw");
                    PonerLongitud(tipo, p.V("k") ?? 0, null, "k");
                    if (p.RadioIn != null) PonerLongitud(tipo, p.RadioIn.Value, BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_WEBFILLET, "r", "r1");
                    break;
                case Forma.TuboRectangular:
                    Medida("altura h", p.V("h"), BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT, "d", "h", "H");
                    Medida("ancho b", p.V("b"), BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH, "b", "bf", "B");
                    Medida("espesor t", p.V("tnom"), BuiltInParameter.STRUCTURAL_SECTION_PIPESTANDARD_WALLNOMINALTHICKNESS, "t", "tnom");
                    PonerLongitud(tipo, p.V("tdes") ?? 0, BuiltInParameter.STRUCTURAL_SECTION_PIPESTANDARD_WALLDESIGNTHICKNESS, "tdes", "t_des");
                    PonerLongitud(tipo, 2 * (p.V("tdes") ?? 0), BuiltInParameter.STRUCTURAL_SECTION_HSS_OUTERFILLET, "ro");
                    PonerLongitud(tipo, p.V("tdes") ?? 0, BuiltInParameter.STRUCTURAL_SECTION_HSS_INNERFILLET, "ri");
                    break;
                default:
                    Medida("diámetro OD", p.V("OD"), BuiltInParameter.STRUCTURAL_SECTION_COMMON_DIAMETER, "OD", "D", "d");
                    Medida("espesor t", p.V("tnom"), BuiltInParameter.STRUCTURAL_SECTION_PIPESTANDARD_WALLNOMINALTHICKNESS, "t", "tnom");
                    PonerLongitud(tipo, p.V("tdes") ?? 0, BuiltInParameter.STRUCTURAL_SECTION_PIPESTANDARD_WALLDESIGNTHICKNESS, "tdes", "t_des");
                    break;
            }

            // Propiedades de análisis: se ponen si la familia lo permite; si no, no importa.
            Poner(tipo, p.V("A"), UnitTypeId.SquareInches, BuiltInParameter.STRUCTURAL_SECTION_AREA, "A");
            Poner(tipo, p.V("wt"), UnitTypeId.PoundsMassPerFoot, BuiltInParameter.STRUCTURAL_SECTION_COMMON_NOMINAL_WEIGHT, "W", "wt", "w");
            Poner(tipo, p.V("Ix"), UnitTypeId.InchesToTheFourthPower, BuiltInParameter.STRUCTURAL_SECTION_COMMON_MOMENT_OF_INERTIA_STRONG_AXIS, "Ix");
            Poner(tipo, p.V("Iy"), UnitTypeId.InchesToTheFourthPower, BuiltInParameter.STRUCTURAL_SECTION_COMMON_MOMENT_OF_INERTIA_WEAK_AXIS, "Iy");
            Poner(tipo, p.V("Sx"), UnitTypeId.CubicInches, BuiltInParameter.STRUCTURAL_SECTION_COMMON_ELASTIC_MODULUS_STRONG_AXIS, "Sx");
            Poner(tipo, p.V("Sy"), UnitTypeId.CubicInches, BuiltInParameter.STRUCTURAL_SECTION_COMMON_ELASTIC_MODULUS_WEAK_AXIS, "Sy");
            Poner(tipo, p.V("Zx"), UnitTypeId.CubicInches, BuiltInParameter.STRUCTURAL_SECTION_COMMON_PLASTIC_MODULUS_STRONG_AXIS, "Zx");
            Poner(tipo, p.V("Zy"), UnitTypeId.CubicInches, BuiltInParameter.STRUCTURAL_SECTION_COMMON_PLASTIC_MODULUS_WEAK_AXIS, "Zy");
            Poner(tipo, p.V("J"), UnitTypeId.InchesToTheFourthPower, BuiltInParameter.STRUCTURAL_SECTION_COMMON_TORSIONAL_MOMENT_OF_INERTIA, "J");
            Poner(tipo, p.V("Cw"), UnitTypeId.InchesToTheSixthPower, BuiltInParameter.STRUCTURAL_SECTION_COMMON_WARPING_CONSTANT, "Cw");
            return faltantes;
        }

        private static bool PonerLongitud(FamilySymbol tipo, double pulgadas, BuiltInParameter? bip, params string[] nombres)
            => pulgadas > 0 && Poner(tipo, pulgadas, UnitTypeId.Inches, bip, nombres);

        /// <summary>Pone un valor en el parámetro integrado o en el primero de los parámetros de familia que exista.</summary>
        private static bool Poner(FamilySymbol tipo, double? valor, ForgeTypeId unidad, BuiltInParameter? bip, params string[] nombres)
        {
            if (valor == null) return false;
            double interno = UnitUtils.ConvertToInternalUnits(valor.Value, unidad);
            bool puesto = false;
            if (bip != null) puesto |= Asignar(tipo.get_Parameter(bip.Value), interno);
            foreach (string nombre in nombres)
                puesto |= Asignar(tipo.LookupParameter(nombre), interno);
            return puesto;
        }

        private static bool Asignar(Parameter param, double valor)
        {
            if (param == null || param.IsReadOnly || param.StorageType != StorageType.Double) return false;
            try { return param.Set(valor); }
            catch { return false; }
        }
    }
}
