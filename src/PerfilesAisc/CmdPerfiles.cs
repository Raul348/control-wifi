using System;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PerfilesAisc.Core;
using PerfilesAisc.Revit;
using PerfilesAisc.UI;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace PerfilesAisc
{
    /// <summary>
    /// Abre la ventana de perfiles. Al pulsar "Colocar viga" o "Colocar columna" se cierran la ventana,
    /// se colocan tantas piezas como quiera el usuario (Esc para terminar) y se vuelve a abrir la ventana.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class CmdPerfiles : IExternalCommand
    {
        /// <summary>Último perfil elegido, para que la ventana lo recuerde en la misma sesión.</summary>
        private static string _ultimo = Ajustes.Leer("perfil", "W8X31");

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document.IsFamilyDocument)
            {
                TaskDialog.Show(App.Name, "Abra un proyecto de Revit.");
                return Result.Cancelled;
            }

            try
            {
                var ventana = new RevitWindow(data.Application.MainWindowHandle);
                while (true)
                {
                    PerfilForm.Accion accion;
                    Perfil perfil;
                    using (var f = new PerfilForm(_ultimo))
                    {
                        if (f.ShowDialog(ventana) != DialogResult.OK) return Result.Succeeded;
                        accion = f.AccionElegida;
                        perfil = f.PerfilElegido;
                    }
                    if (perfil == null) continue;
                    _ultimo = perfil.Nombre;
                    Ajustes.Guardar("perfil", _ultimo);

                    var colocador = new Colocador(uidoc);
                    string resultado;
                    if (accion == PerfilForm.Accion.VigaCurva)
                    {
                        double flecha;
                        bool vertical;
                        using (var a = new ArcoForm(perfil.Nombre, Ajustes.Leer("flecha", 2.0)))
                        {
                            if (a.ShowDialog(ventana) != DialogResult.OK) continue;
                            flecha = a.FlechaM;
                            vertical = a.Vertical;
                        }
                        Ajustes.Guardar("flecha", flecha);
                        resultado = colocador.ColocarVigasCurvas(perfil, flecha, vertical);
                    }
                    else if (accion == PerfilForm.Accion.Viga) resultado = colocador.ColocarVigas(perfil);
                    else resultado = colocador.ColocarColumnas(perfil);
                    if (!string.IsNullOrEmpty(resultado))
                        TaskDialog.Show(App.Name, resultado);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show(App.Name, "Error inesperado:\n" + ex.Message);
                return Result.Failed;
            }
        }
    }

    /// <summary>Ventana de Revit como dueña de la ventana del programa, para que quede encima.</summary>
    public sealed class RevitWindow : IWin32Window
    {
        public RevitWindow(IntPtr handle) { Handle = handle; }
        public IntPtr Handle { get; }
    }
}
