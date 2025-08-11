using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;

namespace MaterRevitAddin.Utils
{
    public class PreviewState
    {
        public ElementId TempAppearanceId = ElementId.InvalidElementId;
        public Dictionary<ElementId, ElementId> MaterialOldApp = new(); // materialId -> oldAppearanceId
    }

    public static class PreviewManager
    {
        private static PreviewState? _current;
        public static void Cancel(Document doc)
        {
            if (_current == null) return;
            using var t = new Transaction(doc, "Cancel Preview");
            t.Start();
            foreach (var kv in _current.MaterialOldApp)
            {
                var mat = doc.GetElement(kv.Key) as Material;
                if (mat != null) mat.AppearanceAssetId = kv.Value;
            }
            if (_current.TempAppearanceId != ElementId.InvalidElementId)
            {
                var temp = doc.GetElement(_current.TempAppearanceId);
                if (temp != null) doc.Delete(temp.Id);
            }
            t.Commit();
            _current = null;
        }

        public static void Start(Document doc, IEnumerable<Material> materialsToPreview, Action<Asset> configureAsset, string baseName = "PREVIEW_TEMP")
        {
            Cancel(doc);
            using var t = new Transaction(doc, "Start Preview");
            t.Start();

            var generic = new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>().FirstOrDefault(a => a.Name.Equals("Generic", StringComparison.OrdinalIgnoreCase));
            if (generic == null) throw new InvalidOperationException("Apparence 'Generic' introuvable.");
            var tempName = NameUtils.GetUniqueAppearanceName(doc, baseName);
            var temp = generic.Duplicate(tempName);
            using (var aes = new AppearanceAssetEditScope(doc))
            {
                var a = (Asset)aes.Start(temp.Id);
                configureAsset(a);
                aes.Commit(true);
            }

            var state = new PreviewState { TempAppearanceId = temp.Id };
            foreach (var m in materialsToPreview)
            {
                state.MaterialOldApp[m.Id] = m.AppearanceAssetId;
                m.AppearanceAssetId = temp.Id;
            }
            _current = state;

            t.Commit();
        }
    }
}
