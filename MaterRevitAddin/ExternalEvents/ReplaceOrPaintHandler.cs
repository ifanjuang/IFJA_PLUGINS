using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Linq;
using MaterRevitAddin.ViewModels;
using MaterRevitAddin.Utils;

namespace MaterRevitAddin.ExternalEvents
{
    public class ReplaceOrPaintHandler : IExternalEventHandler
    {
        public static ExternalEvent Event { get; } = ExternalEvent.Create(new ReplaceOrPaintHandler());
        public static ReplaceOrPaintHandler Instance { get; } = new ReplaceOrPaintHandler();
        public MaterViewModel? VM { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            if (VM == null) return;

            var appe = new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement)).Cast<AppearanceAssetElement>()
                .FirstOrDefault(a => a.Name.Equals(VM.AppearanceName, System.StringComparison.OrdinalIgnoreCase));
            if (appe == null) { TaskDialog.Show("Remplacer", "Apparence introuvable."); return; }

            if (AppearanceComparer.IsNoOp(appe, VM))
            {
                var td = new TaskDialog("Aucune modification")
                { MainInstruction = "Aucune différence détectée.", MainContent = "Passer en mode Peinture ?" };
                td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                if (td.Show() == TaskDialogResult.Yes)
                {
                    var mid = VM.ResolveTargetMaterialId(doc);
                    StartPaintSessionHandler.Start(app, VM, mid);
                }
                return;
            }

            CreateOrUpdateMaterialHandler.Instance.VM = VM;
            CreateOrUpdateMaterialHandler.Event.Raise();

            var td2 = new TaskDialog("Remplacer")
            { MainInstruction = "Apparence mise à jour.", MainContent = "Peindre des faces maintenant ?" };
            td2.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            if (td2.Show() == TaskDialogResult.Yes)
            {
                var mid = VM.ResolveTargetMaterialId(doc);
                StartPaintSessionHandler.Start(app, VM, mid);
            }
        }

        public string GetName() => "Replace or Paint";
    }
}
