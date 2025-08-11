using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace MaterRevitAddin.ExternalEvents
{
    public class StartPaintSessionHandler : IExternalEventHandler
    {
        public static ExternalEvent Event { get; } = ExternalEvent.Create(new StartPaintSessionHandler());
        public static StartPaintSessionHandler Instance { get; } = new StartPaintSessionHandler();
        public MaterRevitAddin.ViewModels.MaterViewModel? VM { get; set; }

        public void Execute(UIApplication app)
        {
            var mid = VM?.ResolveTargetMaterialId(app.ActiveUIDocument.Document) ?? ElementId.InvalidElementId;
            Start(app, VM!, mid);
        }

        public static void Start(UIApplication uiapp, MaterRevitAddin.ViewModels.MaterViewModel vm, ElementId materialId)
        {
            var doc = uiapp.ActiveUIDocument.Document;
            using var tg = new TransactionGroup(doc, "Preview + Paint");
            tg.Start();
            var uidoc = uiapp.ActiveUIDocument;

            try
            {
                while (true)
                {
                    Reference? r = null;
                    try { r = uidoc.Selection.PickObject(ObjectType.Face, "Sélectionnez des faces (ESC pour terminer)"); }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException) { break; }
                    if (r == null) break;

                    var el = doc.GetElement(r);
                    var face = el.GetGeometryObjectFromReference(r) as Face;

                    using var t = new Transaction(doc, "Paint");
                    t.Start();
                    bool done = false;
                    try { if (face != null) { doc.Paint(el.Id, face, materialId); done = true; } } catch { }

                    if (!done) done = TryPaintAllFaces(doc, el, materialId);
                    if (!done)
                    {
                        var res = MaterRevitAddin.Utils.MaterialParamFinder.GetEditableMaterialParams(doc, el);
                        Autodesk.Revit.DB.Parameter? chosen = null;
                        if (res.instanceParams.Count > 0) chosen = res.instanceParams[0];
                        else if (res.typeParams.Count > 0) chosen = res.typeParams[0];
                        if (chosen != null) { chosen.Set(materialId); done = true; }
                    }
                    t.Commit();
                }

                var td = new TaskDialog("Peinture") { MainInstruction = "Terminer ?", MainContent = "Accepter (Oui) ou Annuler (Non) ?", CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No };
                if (td.Show() == TaskDialogResult.Yes) tg.Assimilate(); else tg.RollBack();
            }
            catch { tg.RollBack(); throw; }
        }

        static bool TryPaintAllFaces(Document doc, Element el, ElementId mid)
        {
            var opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
            var geo = el.get_Geometry(opt);
            if (geo == null) return false;
            bool any = false;
            foreach (var obj in geo)
            {
                if (obj is Solid s && s.Faces.Size > 0)
                {
                    foreach (Face f in s.Faces)
                    {
                        try { doc.Paint(el.Id, f, mid); any = true; } catch { }
                    }
                }
            }
            return any;
        }

        public string GetName() => "Paint Session";
    }
}
