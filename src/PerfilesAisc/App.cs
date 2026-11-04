using System;
using System.Reflection;
using Autodesk.Revit.UI;
using PerfilesAisc.UI;

namespace PerfilesAisc
{
    /// <summary>Agrega el botón "Perfiles AISC" a la pestaña ACERO de la cinta de Revit (la crea si no existe).</summary>
    public sealed class App : IExternalApplication
    {
        public const string Name = "Perfiles AISC";
        public const string TabName = "ACERO";

        public Result OnStartup(UIControlledApplication app)
        {
            try { app.CreateRibbonTab(TabName); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { /* la pestaña ya existe (por ejemplo, la creó ACERO Refuerzo) */ }

            string path = Assembly.GetExecutingAssembly().Location;
            RibbonPanel panel = app.CreateRibbonPanel(TabName, "Perfiles de acero");
            var boton = new PushButtonData("PerfilesAisc", "Perfiles\nAISC", path, typeof(CmdPerfiles).FullName)
            {
                ToolTip = "Catálogo de perfiles AISC: W, HP, M, S, C, MC, WT, L, 2L, HSS y Pipe.",
                LongDescription = "Elija el perfil en el árbol, como en Tekla, vea sus medidas y propiedades, " +
                                  "y colóquelo en el modelo como viga o columna. Si el tipo no existe en el proyecto, se crea con sus medidas.",
                LargeImage = Iconos.Cinta(32),
                Image = Iconos.Cinta(16),
            };
            panel.AddItem(boton);

            var nave = new PushButtonData("PerfilesAiscNave", "Nave\ncurva", path, typeof(CmdNave).FullName)
            {
                ToolTip = "Nave industrial con cubierta en arco: columnas, arcos, correas y arriostres.",
                LongDescription = "Indique luz, altura, flecha, número de pórticos y separación. El arco puede ser una viga curva, " +
                                  "tramos rectos o una celosía en arco. Elija los perfiles del catálogo y dos puntos en el modelo.",
                LargeImage = Iconos.CintaNave(32),
                Image = Iconos.CintaNave(16),
            };
            panel.AddItem(nave);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
