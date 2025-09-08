using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Mater2026.Services
{
    /// <summary>
    /// Temporary view-level highlighting of elements (cleared on next selection/deselect).
    /// </summary>
    public static class TemporaryHighlightService
    {
        private static readonly Dictionary<ElementId, IList<ElementId>> _byView = new();

        public static void Apply(View view, IList<ElementId> ids)
        {
            if (view == null || ids == null || ids.Count == 0) return;

            Clear(view);

            var ogs = new OverrideGraphicSettings();

            var c = new Color(80, 180, 255);
            ogs.SetProjectionLineColor(c);
            ogs.SetSurfaceBackgroundPatternColor(c);
            ogs.SetSurfaceForegroundPatternColor(c);

            var solid = new FilteredElementCollector(view.Document)
                        .OfClass(typeof(FillPatternElement))
                        .FirstOrDefault() as FillPatternElement;
            if (solid != null)
            {
                ogs.SetSurfaceForegroundPatternId(solid.Id);
                ogs.SetSurfaceBackgroundPatternId(solid.Id);
            }

            foreach (var id in ids)
                view.SetElementOverrides(id, ogs);

            _byView[view.Id] = new List<ElementId>(ids);
        }

        public static void Clear(View view)
        {
            if (view == null) return;
            if (!_byView.TryGetValue(view.Id, out var last)) return;

            var clear = new OverrideGraphicSettings();
            foreach (var id in last)
                view.SetElementOverrides(id, clear);

            _byView.Remove(view.Id);
        }
    }
}
