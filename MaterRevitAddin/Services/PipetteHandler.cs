using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
// put near other using lines
using WpfApp = System.Windows.Application;
using SMessageBox = System.Windows.MessageBox;

namespace Mater2026.Services
{
    public class PipetteHandler : IExternalEventHandler
    {
        public UIDocument? UiDoc { get; set; }
        public System.Action<ElementId>? OnPicked { get; set; }
        public System.Action? OnBegin { get; set; }
        public System.Action<bool>? OnEnd { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = UiDoc ?? app.ActiveUIDocument;
            if (uidoc == null) { OnEnd?.Invoke(false); return; }
            var doc = uidoc.Document;

            OnBegin?.Invoke();
            bool success = false;
            try
            {
                var r = uidoc.Selection.PickObject(ObjectType.Face, "PIPETTE : cliquez une face");
                if (r == null) { OnEnd?.Invoke(false); return; }

                var matId = MaterialPickService.SampleFromReference(doc, r);
                if (matId != null && matId != ElementId.InvalidElementId)
                {
                    OnPicked?.Invoke(matId);
                    success = true;
                }
                else
                {
                    SMessageBox.Show("Aucun matériau détecté sur la sélection.", "Pipette",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch { }
            finally { OnEnd?.Invoke(success); }
        }

        public string GetName() => "Mater2026.PipetteHandler";
    }
}
