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
                uidoc.Selection.SetElementIds([]);
                TemporaryHighlightService.Clear(uidoc.ActiveView);
                return;
            }

            var mat = VM?.SelectedMaterial;
            if (mat == null)
            {
                uidoc.Selection.SetElementIds([]);
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
                    bool has = false;

                    var baseIds = e.GetMaterialIds(false);     // matériaux "de base"
                    if (baseIds != null && baseIds.Contains(mat.Id))
                        has = true;
                    else
                    {
                        var paintedIds = e.GetMaterialIds(true); // matériaux peints
                        if (paintedIds != null && paintedIds.Contains(mat.Id))
                            has = true;
                    }

                    if (has) ids.Add(e.Id);
                }
                catch
                {
                    // certains éléments ne supportent pas GetMaterialIds
                }
            }


            uidoc.Selection.SetElementIds(ids);
            TemporaryHighlightService.Apply(uidoc.ActiveView, ids);
            MaterViewModel.LogInfo($"Selection: {ids.Count} element(s) use \"{mat.Name}\".");
        }

        public string GetName() => "Mater2026.SelectByAppearance";
    }
}
