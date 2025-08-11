using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using System.Linq;
using MaterRevitAddin.ViewModels;
using MaterRevitAddin.Models;
using MaterRevitAddin.Utils;
using System;

namespace MaterRevitAddin.ExternalEvents
{
    public class CreateOrUpdateMaterialHandler : IExternalEventHandler
    {
        public static ExternalEvent Event { get; } = ExternalEvent.Create(new CreateOrUpdateMaterialHandler());
        public static CreateOrUpdateMaterialHandler Instance { get; } = new CreateOrUpdateMaterialHandler();
        public MaterViewModel? VM { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            if (VM == null) return;

            using var t = new Transaction(doc, "Create/Update Material");
            t.Start();

            var mat = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .FirstOrDefault(x => x.Name.Equals(VM.MaterialName, StringComparison.OrdinalIgnoreCase))
                ?? Material.Create(doc, VM.MaterialName);

            var appe = new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement)).Cast<AppearanceAssetElement>()
                .FirstOrDefault(x => x.Name.Equals(VM.AppearanceName, StringComparison.OrdinalIgnoreCase));

            if (appe == null)
            {
                var generic = new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement))
                    .Cast<AppearanceAssetElement>().FirstOrDefault(a => a.Name.Equals("Generic", StringComparison.OrdinalIgnoreCase));
                if (generic == null) throw new Exception("Apparence 'Generic' introuvable.");
                appe = generic.Duplicate(VM.AppearanceName);
            }

            appe.Description = VM.Description ?? "";
            mat.AppearanceAssetId = appe.Id;

            using var aes = new AppearanceAssetEditScope(doc);
            var newAsset = aes.Start(appe.Id);
            var asset = (Asset)newAsset;

            AssetUtils.SetBitmapProperty(asset, "generic_diffuse", VM.GetSlotPath(MapType.DIFF),
                VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);

            var glosPath = VM.GetSlotPath(MapType.GLOS);
            bool invert = !string.IsNullOrEmpty(glosPath) && MapFileUtils.Detect(glosPath).label.ToLower().Contains("rough");
            AssetUtils.SetBitmapProperty(asset, "generic_glossiness", glosPath,
                VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, invert);

            AssetUtils.SetEnum(asset, "generic_reflectivity_function", "fresnel");
            AssetUtils.SetBitmapProperty(asset, "generic_reflectivity_at_90deg", VM.GetSlotPath(MapType.REFL),
                VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);

            AssetUtils.SetBitmapProperty(asset, "generic_bump", VM.GetSlotPath(MapType.BUMP),
                VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);

            AssetUtils.SetBitmapProperty(asset, "generic_transparency", VM.GetSlotPath(MapType.OPAC),
                VM.RealWorldSizeX, VM.RealWorldSizeY, VM.RotationAngle, false);

            aes.Commit(true);
            t.Commit();
        }

        public string GetName() => "Create/Update Material";
    }
}
