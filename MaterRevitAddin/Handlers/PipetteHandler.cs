using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Mater2026.Handlers
{
    /// <summary>
    /// Single-face pick eyedropper. Returns the material id (painted wins).
    /// </summary>
    public class PipetteHandler : IExternalEventHandler
    {
        public UIDocument? UiDoc { get; set; }
        public Action<ElementId>? OnPicked { get; set; }
        public Action? OnBegin { get; set; }
        public Action<bool>? OnEnd { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = UiDoc ?? app.ActiveUIDocument;
            if (uidoc == null) return;
            var doc = uidoc.Document;

            OnBegin?.Invoke();
            bool ok = false;

            try
            {
                var r = uidoc.Selection.PickObject(ObjectType.Face, "Eyedrop: pick a face (Esc to cancel)");
                if (r == null) return;

                var el = doc.GetElement(r.ElementId);
                var face = el?.GetGeometryObjectFromReference(r) as Face;
                if (face == null) return;

                var matId = SampleMaterialId(doc, r.ElementId, face);
                if (matId != ElementId.InvalidElementId)
                {
                    OnPicked?.Invoke(matId);
                    ok = true;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // user cancelled
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater2026 – Eyedrop", ex.Message);
            }
            finally
            {
                OnEnd?.Invoke(ok);
            }
        }

        private static ElementId SampleMaterialId(Document doc, ElementId elemId, Face face)
        {
            var painted = doc.GetPaintedMaterial(elemId, face);
            if (painted != ElementId.InvalidElementId) return painted;

            var baseMat = face.MaterialElementId;
            if (baseMat != ElementId.InvalidElementId) return baseMat;

            var el = doc.GetElement(elemId);
            var ids = el?.GetMaterialIds(true);
            if (ids != null && ids.Count > 0) return ids.First();
            ids = el?.GetMaterialIds(false);
            if (ids != null && ids.Count > 0) return ids.First();

            return ElementId.InvalidElementId;
        }

        public string GetName() => "Mater2026.Pipette";
    }
}
