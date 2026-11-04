using System;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PerfilesAisc.Revit;
using PerfilesAisc.UI;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace PerfilesAisc
{
    /// <summary>Genera una nave industrial con cubierta en arco: columnas, arcos (curvos, por tramos o en celosía), correas y arriostres.</summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class CmdNave : IExternalCommand
    {
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
                using (var f = new NaveForm())
                {
                    if (f.ShowDialog(new RevitWindow(data.Application.MainWindowHandle)) != DialogResult.OK) return Result.Cancelled;
                    string resultado = new Colocador(uidoc).GenerarNave(f.Parametros, f.Perfiles, f.OtroLado);
                    if (!string.IsNullOrEmpty(resultado)) TaskDialog.Show(App.Name, resultado);
                }
                return Result.Succeeded;
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
}
