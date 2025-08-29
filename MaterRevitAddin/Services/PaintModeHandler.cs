using System.Collections.Generic;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SMessageBox = System.Windows.MessageBox;

namespace Mater2026.Services
{
    public class PaintModeHandler : IExternalEventHandler
    {
        public UIDocument? UiDoc { get; set; }
        public ElementId CurrentMaterialId { get; set; } = ElementId.InvalidElementId;
        public System.Action<ElementId>? OnMaterialSampled { get; set; }
        public System.Action? OnBegin { get; set; }
        public System.Action<bool>? OnEnd { get; set; }

        private record FaceKey(ElementId ElementId, string StableRef);
        private record PaintAction(FaceKey Key, bool WasPainted, ElementId? PrevMatId, ElementId? NewMatId);
        private readonly Stack<PaintAction> _undo = new();
        private readonly Stack<PaintAction> _redo = new();

        public void Execute(UIApplication app)
        {
            var uidoc = UiDoc ?? app?.ActiveUIDocument;
            if (uidoc is null) { OnEnd?.Invoke(false); return; }

            var doc = uidoc.Document;
            OnBegin?.Invoke();
            bool cancelAll = false;

            try
            {
                while (true)
                {
                    const string prompt =
                        "ASSIGNER : clic=peindre | ALT+clic=pipette | SHIFT+clic=dépeindre | " +
                        "CTRL+Z annuler | CTRL+Y rétablir | Entrée/clic droit=terminer | Échap=annuler";

                    Reference? r = null;
                    try
                    {
                        r = uidoc.Selection.PickObject(ObjectType.Face, prompt);
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // First, check undo/redo chords while in pick
                        if (IsCtrlZ()) { TryUndo(doc); continue; }
                        if (IsCtrlY()) { TryRedo(doc); continue; }

                        // Then ask whether to finish, continue or rollback
                        var res = SMessageBox.Show(
                            "Terminer la session d’ASSIGNER ?\n\n" +
                            "Oui = Valider et quitter\n" +
                            "Non = Continuer\n" +
                            "Annuler = Annuler toutes les modifications",
                            "Terminer ASSIGNER",
                            System.Windows.MessageBoxButton.YesNoCancel,
                            System.Windows.MessageBoxImage.Question,
                            System.Windows.MessageBoxResult.No);

                        if (res == System.Windows.MessageBoxResult.Yes) break;           // keep changes
                        if (res == System.Windows.MessageBoxResult.Cancel) { cancelAll = true; break; } // rollback
                        continue; // No -> keep painting
                    }

                    var elem = doc.GetElement(r);
                    var face = elem?.GetGeometryObjectFromReference(r) as Face;
                    if (elem == null || face == null) continue;

                    bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
                    bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                    if (alt)
                    {
                        var sampled = MaterialPickService.SampleFromReference(doc, r);
                        if (sampled != null && sampled != ElementId.InvalidElementId)
                        {
                            CurrentMaterialId = sampled;
                            OnMaterialSampled?.Invoke(CurrentMaterialId);
                        }
                        continue;
                    }

                    if (IsCtrlZ()) { TryUndo(doc); continue; }
                    if (IsCtrlY()) { TryRedo(doc); continue; }
                    if (CurrentMaterialId == ElementId.InvalidElementId) continue;

                    var key = new FaceKey(elem.Id, r.ConvertToStableRepresentation(doc));
                    bool wasPainted = doc.IsPainted(elem.Id, face);
                    ElementId? prevMatId = wasPainted ? doc.GetPaintedMaterial(elem.Id, face) : null;

                    if (shift)
                    {
                        using var tx = new Transaction(doc, "Unpaint Face");
                        tx.Start();
                        doc.RemovePaint(elem.Id, face);
                        tx.Commit();
                        PushAction(new PaintAction(key, wasPainted, prevMatId, null));
                    }
                    else
                    {
                        using var tx = new Transaction(doc, "Paint Face");
                        tx.Start();
                        doc.Paint(elem.Id, face, CurrentMaterialId);
                        tx.Commit();
                        PushAction(new PaintAction(key, wasPainted, prevMatId, CurrentMaterialId));
                    }
                }
            }
            catch
            {
                cancelAll = true;
            }
            finally
            {
                if (cancelAll) TryRollbackAll(doc);
                OnEnd?.Invoke(!cancelAll);
            }
        }

        public string GetName() => "Mater2026.PaintModeHandler";

        private static bool IsCtrlZ() =>
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && Keyboard.IsKeyDown(Key.Z);

        private static bool IsCtrlY() =>
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && Keyboard.IsKeyDown(Key.Y);

        private void PushAction(PaintAction a) { _undo.Push(a); _redo.Clear(); }
        private void TryUndo(Document doc) { if (_undo.Count == 0) return; var a = _undo.Pop(); ApplyReverse(doc, a); _redo.Push(a); }
        private void TryRedo(Document doc) { if (_redo.Count == 0) return; var a = _redo.Pop(); ApplyForward(doc, a); _undo.Push(a); }
        private void TryRollbackAll(Document doc) { while (_undo.Count > 0) { var a = _undo.Pop(); ApplyReverse(doc, a); } _redo.Clear(); }

        private static void ApplyForward(Document doc, PaintAction a)
        {
            var (elem, face) = ResolveFace(doc, a.Key);
            if (elem == null || face == null) return;
            using var tx = new Transaction(doc, "Redo Paint");
            tx.Start();
            if (a.NewMatId != null) doc.Paint(elem.Id, face, a.NewMatId);
            else doc.RemovePaint(elem.Id, face);
            tx.Commit();
        }

        private static void ApplyReverse(Document doc, PaintAction a)
        {
            var (elem, face) = ResolveFace(doc, a.Key);
            if (elem == null || face == null) return;
            using var tx = new Transaction(doc, "Undo Paint");
            tx.Start();
            if (a.WasPainted && a.PrevMatId != null) doc.Paint(elem.Id, face, a.PrevMatId);
            else doc.RemovePaint(elem.Id, face);
            tx.Commit();
        }

        private static (Element? elem, Face? face) ResolveFace(Document doc, FaceKey key)
        {
            try
            {
                var elem = doc.GetElement(key.ElementId);
                var r = Reference.ParseFromStableRepresentation(doc, key.StableRef);
                var face = elem?.GetGeometryObjectFromReference(r) as Face;
                return (elem, face);
            }
            catch { return (null, null); }
        }
    }
}
