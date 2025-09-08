using System;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Mater2026.Handlers
{
    /// <summary>
    /// Interactive painting mode:
    /// - Click = Paint with CurrentMaterialId
    /// - SHIFT+Click = Unpaint
    /// - CTRL+Click = Eyedrop material (painted wins) -> OnMaterialSampled
    /// - ESC / right-click = exit
    /// </summary>
    public class PaintModeHandler : IExternalEventHandler
    {
        public UIDocument? UiDoc { get; set; }
        public ElementId CurrentMaterialId { get; set; } = ElementId.InvalidElementId;

        public Action? OnBegin { get; set; }
        public Action<bool>? OnEnd { get; set; }
        public Action<ElementId>? OnMaterialSampled { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = UiDoc ?? app.ActiveUIDocument;
            if (uidoc == null) return;
            var doc = uidoc.Document;

            OnBegin?.Invoke();
            bool anyOp = false;

            try
            {
                while (true)
                {
                    var prompt = "Face: Click=Paint · SHIFT=Unpaint · CTRL=Eyedrop · Esc=Exit";
                    var r = uidoc.Selection.PickObject(ObjectType.Face, prompt);
                    if (r == null) break;

                    var el = doc.GetElement(r.ElementId);
                    var face = el?.GetGeometryObjectFromReference(r) as Face;
                    if (face == null) continue;

                    // CTRL → Eyedropper
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        var sampled = SampleMaterialId(doc, r.ElementId, face);
                        if (sampled != ElementId.InvalidElementId)
                            OnMaterialSampled?.Invoke(sampled);
                        continue;
                    }

                    using var t = new Transaction(doc, ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) ? "Unpaint" : "Paint");
                    t.Start();

                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    {
                        doc.RemovePaint(r.ElementId, face);
                    }
                    else
                    {
                        if (CurrentMaterialId == ElementId.InvalidElementId)
                        {
                            t.RollBack();
                            Autodesk.Revit.UI.TaskDialog.Show("Mater2026", "No material selected to paint.");
                            continue;
                        }
                        doc.Paint(r.ElementId, face, CurrentMaterialId);
                    }

                    t.Commit();
                    anyOp = true;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // ESC / right-click → exit
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Mater2026 – Paint", ex.Message);
            }
            finally
            {
                OnEnd?.Invoke(anyOp);
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

        public string GetName() => "Mater2026.PaintMode";
    }
}
