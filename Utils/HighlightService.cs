using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MaterRevitAddin.Utils
{
    public static class HighlightService
    {
        public static void HighlightByMaterials(UIApplication uiapp, ICollection<ElementId> matIds)
        {
            var doc = uiapp.ActiveUIDocument.Document;
            var view = doc.ActiveView;
            var collector = new FilteredElementCollector(doc, view.Id);
            var elems = new List<ElementId>();
            foreach (var e in collector)
            {
                try
                {
                    var set = e.GetMaterialIds(true);
                    foreach (var m in set)
                    {
                        if (matIds.Contains(m)) { elems.Add(e.Id); break; }
                    }
                }
                catch { }
            }
            uiapp.ActiveUIDocument.Selection.SetElementIds(elems);
        }
    }
}
