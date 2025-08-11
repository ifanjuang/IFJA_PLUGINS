using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using MaterRevitAddin.ViewModels;
using MaterRevitAddin.Utils;
using MaterRevitAddin.Models;

namespace MaterRevitAddin.ExternalEvents
{
    public class BatchReplaceNewHandler : IExternalEventHandler
    {
        public static ExternalEvent Event { get; } = ExternalEvent.Create(new BatchReplaceNewHandler());
        public static BatchReplaceNewHandler Instance { get; } = new BatchReplaceNewHandler();
        public MaterViewModel? VM { get; set; }

        public void Execute(UIApplication app)
        {
            if (VM == null) return;
            var doc = app.ActiveUIDocument.Document;

            var selectedMats = VM.ProjectMaterials.Where(x => x.IsSelected).Select(x => doc.GetElement(x.Id) as Material).Where(m => m != null).Cast<Material>().ToList();
            var selectedApps = VM.ProjectAppearances.Where(x => x.IsSelected).Select(x => doc.GetElement(x.Id) as AppearanceAssetElement).Where(a => a != null).Cast<AppearanceAssetElement>().ToList();

            var mats = new List<Material>();
            if (selectedMats.Count > 0) mats.AddRange(selectedMats);
            if (selectedApps.Count > 0)
            {
                var matAll = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>();
                mats.AddRange(matAll.Where(m => selectedApps.Any(a => a.Id == m.AppearanceAssetId)));
            }
            mats = mats.Distinct(new IdComparer<Material>()).ToList();
            if (mats.Count == 0) { TaskDialog.Show("Remplacer", "Aucun matériau sélectionné."); return; }

            using var t = new Transaction(doc, "Batch Replace (New Appearance)");
            t.Start();
            var generic = new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>().FirstOrDefault(a => a.Name.Equals("Generic", StringComparison.OrdinalIgnoreCase));
            if (generic == null) throw new Exception("Apparence 'Generic' introuvable.");
            var newName = NameUtils.GetUniqueAppearanceName(doc, VM.AppearanceName);
            var appe = generic.Duplicate(newName);
            appe.Description = VM.Description ?? "";

            using (var aes = new AppearanceAssetEditScope(doc))
            {
                var asset = (Asset)aes.Start(appe.Id);
                AssetUtils.SetBitmapProperty(asset, "generic_diffuse", VM.GetSlotPath(MapType.DIFF), VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);
                var glos = VM.GetSlotPath(MapType.GLOS);
                var invert = !string.IsNullOrEmpty(glos) && MapFileUtils.Detect(glos).label.Contains("Rough");
                AssetUtils.SetBitmapProperty(asset, "generic_glossiness", glos, VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, invert);
                AssetUtils.SetEnum(asset, "generic_reflectivity_function", "fresnel");
                AssetUtils.SetBitmapProperty(asset, "generic_reflectivity_at_90deg", VM.GetSlotPath(MapType.REFL), VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);
                AssetUtils.SetBitmapProperty(asset, "generic_bump", VM.GetSlotPath(MapType.BUMP), VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);
                AssetUtils.SetBitmapProperty(asset, "generic_transparency", VM.GetSlotPath(MapType.OPAC), VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);
                aes.Commit(true);
            }

            foreach (var m in mats) m.AppearanceAssetId = appe.Id;
            t.Commit();

            TaskDialog.Show("Remplacer", $"Assigné la nouvelle apparence '{newName}' à {mats.Count} matériau(x).");
        }

        public string GetName() => "Batch Replace (New)";
    }

    public class BatchOverwriteHandler : IExternalEventHandler
    {
        public static ExternalEvent Event { get; } = ExternalEvent.Create(new BatchOverwriteHandler());
        public static BatchOverwriteHandler Instance { get; } = new BatchOverwriteHandler();
        public MaterViewModel? VM { get; set; }

        public void Execute(UIApplication app)
        {
            if (VM == null) return;
            var doc = app.ActiveUIDocument.Document;

            var selectedApps = VM.ProjectAppearances.Where(x => x.IsSelected).Select(x => doc.GetElement(x.Id) as AppearanceAssetElement).Where(a => a != null).Cast<AppearanceAssetElement>().ToList();
            if (selectedApps.Count == 0) { TaskDialog.Show("Écraser", "Sélectionnez au moins une apparence."); return; }

            using var t = new Transaction(doc, "Batch Overwrite Appearance(s)");
            t.Start();
            foreach (var appe in selectedApps)
            {
                appe.Description = VM.Description ?? "";
                using var aes = new AppearanceAssetEditScope(doc);
                var asset = (Asset)aes.Start(appe.Id);
                AssetUtils.SetBitmapProperty(asset, "generic_diffuse", VM.GetSlotPath(MapType.DIFF), VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);
                var glos = VM.GetSlotPath(MapType.GLOS);
                var invert = !string.IsNullOrEmpty(glos) && MapFileUtils.Detect(glos).label.Contains("Rough");
                AssetUtils.SetBitmapProperty(asset, "generic_glossiness", glos, VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, invert);
                AssetUtils.SetEnum(asset, "generic_reflectivity_function", "fresnel");
                AssetUtils.SetBitmapProperty(asset, "generic_reflectivity_at_90deg", VM.GetSlotPath(MapType.REFL), VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);
                AssetUtils.SetBitmapProperty(asset, "generic_bump", VM.GetSlotPath(MapType.BUMP), VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);
                AssetUtils.SetBitmapProperty(asset, "generic_transparency", VM.GetSlotPath(MapType.OPAC), VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);
                aes.Commit(true);
            }
            t.Commit();
            TaskDialog.Show("Écraser", $"Écrasé {selectedApps.Count} apparence(s).");
        }

        public string GetName() => "Batch Overwrite";
    }

    class IdComparer<T> : IEqualityComparer<T> where T : Element
    {
        public bool Equals(T? x, T? y) => x?.Id.IntegerValue == y?.Id.IntegerValue;
        public int GetHashCode(T obj) => obj.Id.IntegerValue;
    }
}
