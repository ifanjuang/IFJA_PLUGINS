using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Mater2026.ViewModels;

namespace Mater2026.Handlers
{
    public class SelectByAppearanceHandler : IExternalEventHandler
    {
        public MaterViewModel? VM { get; set; }
        public bool ClearSelection { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument; if (uidoc == null) return;

            if (ClearSelection)
            {
                uidoc.Selection.SetElementIds(new List<ElementId>());
                return;
            }

            var mat = VM?.SelectedMaterial;
            if (mat == null) { uidoc.Selection.SetElementIds(new List<ElementId>()); return; }

            var doc = uidoc.Document;
            var ids = new List<ElementId>();

            var elems = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var e in elems)
            {
                try
                {
                    var mids = e.GetMaterialIds(includePainted: true);
                    if (mids != null && mids.Contains(mat.Id))
                        ids.Add(e.Id);
                }
                catch { }
            }

            uidoc.Selection.SetElementIds(ids);
            VM?.LogInfo($"Sélection : {ids.Count} élément(s) utilisent « {mat.Name} ».");
        }

        public string GetName() => "Mater2026.SelectByAppearance";
    }
}
