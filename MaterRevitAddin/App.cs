using Autodesk.Revit.UI;
using System;
using System.Reflection;

namespace MaterRevitAddin
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                const string tab = "Mater";
                try { app.CreateRibbonTab(tab); } catch { }
                var panel = app.CreateRibbonPanel(tab, "PBR Tools");

                var pd = new PushButtonData(
                    "MaterialPainter", "Material\nPainter",
                    Assembly.GetExecutingAssembly().Location,
                    "MaterRevitAddin.Commands.ShowUICommand");

                var btn = panel.AddItem(pd) as PushButton;
                btn!.ToolTip = "Ouvrir la fenêtre Material Painter (Générique).";
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater", "Erreur OnStartup: " + ex.Message);
                return Result.Failed;
            }
        }
        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
