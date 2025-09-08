using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Mater2026.Services;
using Mater2026.ViewModels;

namespace Mater2026.Handlers
{
    /// <summary>
    /// Selects all elements using the selected material's appearance (painted included). Supports Clear.
    /// Also applies a temporary highlight in the active view.
    /// </summary>
    public class SelectByAppearanceHandler : IExternalEventHandler
    {
        public MaterViewModel? VM { get; set; }
        public bool ClearSelection { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument; if (uidoc == null) return;
            var doc = uidoc.Document;

            if (ClearSelection)
            {
                uidoc.Selection.SetElementIds(new List<ElementId>());
                TemporaryHighlightService.Clear(uidoc.ActiveView);
                return;
            }

            var mat = VM?.SelectedMaterial;
            if (mat == null)
            {
                uidoc.Selection.SetElementIds(new List<ElementId>());
                TemporaryHighlightService.Clear(uidoc.ActiveView);
                return;
            }

            // (Optional) category prefilter – speeds up big models
            var cats = new BuiltInCategory[] {
                BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Ceilings, BuiltInCategory.OST_Columns, BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows, BuiltInCategory.OST_GenericModel
            };

            IList<Element> elemsPref = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(new ElementMulticategoryFilter(cats))
                .ToElements();

            var elems = elemsPref.Count > 0
                ? elemsPref
                : new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements();

            var ids = new List<ElementId>();

            foreach (var e in elems)
            {
                try
                {
                    var mids = e.GetMaterialIds(includePainted: true);
                    if (mids != null && mids.Contains(mat.Id)) ids.Add(e.Id);
                }
                catch { /* some elements don't expose GetMaterialIds */ }
            }

            uidoc.Selection.SetElementIds(ids);
            TemporaryHighlightService.Apply(uidoc.ActiveView, ids);
            VM?.LogInfo($"Selection: {ids.Count} element(s) use \"{mat.Name}\".");
        }

        public string GetName() => "Mater2026.SelectByAppearance";
    }
}
