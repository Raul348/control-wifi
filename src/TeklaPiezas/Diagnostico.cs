using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace TeklaPiezas
{
    /// <summary>
    /// Explica por qué el programa no se conecta con Tekla.
    /// La Open API solo se conecta si Tekla es de la misma versión que la API del programa,
    /// y si ambos se ejecutan con los mismos permisos (los dos como administrador, o ninguno).
    /// </summary>
    internal static class Diagnostico
    {
        /// <summary>Versión de Tekla para la que se compiló el programa, por ejemplo 2025.</summary>
        public static int VersionApi => typeof(Tekla.Structures.Model.Model).Assembly.GetName().Version.Major;

        public static bool ProgramaEsAdministrador
        {
            get
            {
                try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
                catch { return false; }
            }
        }

        /// <summary>Un Tekla Structures abierto en el equipo.</summary>
        public sealed class TeklaAbierto
        {
            public int Pid;
            public string Ruta = "";
            public string VersionArchivo = "";
            public int Anio;            // 2025, 2024... 0 si no se pudo saber
            public bool SinAcceso;      // Windows no deja leerlo: casi siempre porque se abrió como administrador
        }

        public static List<TeklaAbierto> TeklasAbiertos()
        {
            var lista = new List<TeklaAbierto>();
            foreach (Process p in Process.GetProcessesByName("TeklaStructures"))
            {
                var t = new TeklaAbierto { Pid = p.Id };
                try
                {
                    t.Ruta = p.MainModule.FileName;
                    FileVersionInfo fv = FileVersionInfo.GetVersionInfo(t.Ruta);
                    t.VersionArchivo = string.IsNullOrWhiteSpace(fv.ProductVersion) ? fv.FileVersion : fv.ProductVersion;
                }
                catch (Win32Exception) { t.SinAcceso = true; }
                catch (InvalidOperationException) { /* el proceso se cerró */ }
                t.Anio = Anio(t.Ruta) ?? Anio(t.VersionArchivo) ?? 0;
                lista.Add(t);
            }
            return lista;
        }

        private static string Probar(Func<string> accion)
        {
            try { return accion() ?? "null"; }
            catch (Exception ex) { return "ERROR " + Detalle(ex); }
        }

        /// <summary>Tipo y mensaje del error, con los errores internos.</summary>
        private static string Detalle(Exception ex)
        {
            var sb = new StringBuilder();
            int n = 0;
            for (Exception e = ex; e != null && n < 4; e = e.InnerException, n++)
            {
                if (n > 0) sb.Append("  <-  ");
                sb.Append(e.GetType().Name).Append(": ").Append(e.Message.Trim());
            }
            return sb.ToString();
        }

        /// <summary>Busca un año 20xx en la ruta ("...\Tekla Structures\2025.0\bin") o en la versión.</summary>
        private static int? Anio(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return null;
            Match m = Regex.Match(texto, @"(?<!\d)(20[1-3]\d)(?!\d)");
            return m.Success ? int.Parse(m.Groups[1].Value) : (int?)null;
        }

        /// <summary>Texto con los datos y la causa más probable.</summary>
        public static string Informe(Exception error = null)
        {
            var sb = new StringBuilder();
            List<TeklaAbierto> teklas = TeklasAbiertos();
            int api = VersionApi;
            bool admin = ProgramaEsAdministrador;

            sb.AppendLine("Programa compilado para: Tekla Structures " + api);
            sb.AppendLine("Programa ejecutado como administrador: " + (admin ? "sí" : "no"));
            if (teklas.Count == 0) sb.AppendLine("Tekla abierto: no se encontró TeklaStructures.exe");
            foreach (TeklaAbierto t in teklas)
            {
                sb.AppendLine("Tekla abierto: " + (t.Anio > 0 ? "versión " + t.Anio : "versión desconocida")
                              + (t.VersionArchivo != "" ? "  (" + t.VersionArchivo + ")" : ""));
                if (t.Ruta != "") sb.AppendLine("   " + t.Ruta);
                if (t.SinAcceso) sb.AppendLine("   Windows no deja leer este proceso.");
            }
            if (error != null) sb.AppendLine("Error: " + Detalle(error));

            // Pruebas directas de la Open API: muestran el error interno que GetConnectionStatus oculta.
            sb.AppendLine();
            sb.AppendLine("Pruebas de la Open API:");
            sb.AppendLine("  Versión de Tekla: " + Probar(() => Tekla.Structures.TeklaStructuresInfo.GetCurrentProgramVersion()));
            sb.AppendLine("  Compilación de Tekla: " + Probar(() => Tekla.Structures.TeklaStructuresInfo.GetBuildNumber()));
            sb.AppendLine("  Model.GetConnectionStatus: " + Probar(() => new Tekla.Structures.Model.Model().GetConnectionStatus().ToString()));
            sb.AppendLine("  Model.GetInfo: " + Probar(() =>
            {
                var info = new Tekla.Structures.Model.Model().GetInfo();
                return info == null ? "null" : "\"" + info.ModelName + "\"";
            }));
            sb.AppendLine("  DLL de la API: " + typeof(Tekla.Structures.Model.Model).Assembly.Location);
            bool sinServicio = sb.ToString().Contains("because it does not exist");

            sb.AppendLine();
            sb.AppendLine("Causa más probable:");
            if (teklas.Count == 0)
                sb.AppendLine("Tekla Structures no está abierto en este equipo.");
            else if (sinServicio)
                sb.AppendLine("Tekla está abierto, pero no está publicando el servicio de la Open API: el canal de conexión no existe. " +
                              "El programa busca el canal correcto para su versión. En una instalación oficial de Tekla este servicio " +
                              "se publica solo al abrir Tekla, y no hay nada que configurar.");
            else if (teklas.Exists(t => t.SinAcceso) && !admin)
                sb.AppendLine("Tekla se abrió como administrador y este programa no. Cierre el programa, haga clic derecho en " +
                              "TeklaPiezas.exe y elija \"Ejecutar como administrador\". O abra Tekla sin permisos de administrador.");
            else if (teklas.Exists(t => t.Anio > 0 && t.Anio != api))
            {
                int anio = teklas.Find(t => t.Anio > 0 && t.Anio != api).Anio;
                sb.AppendLine($"Su Tekla es la versión {anio} y este programa es para la {api}. " +
                              $"Use el programa de la carpeta Tekla{anio}.");
            }
            else
                sb.AppendLine("Tekla está abierto, la versión coincide y los permisos también, pero la Open API no responde. " +
                              "Revise que el modelo haya terminado de abrir y que no haya una ventana de Tekla esperando respuesta. " +
                              "Si sigue igual, las pruebas de arriba muestran el error interno.");
            return sb.ToString();
        }
    }
}
