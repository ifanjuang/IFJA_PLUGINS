using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;

namespace MaterRevitAddin.Utils
{
    public static class AppearanceComparer
    {
        static bool PathEq(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b)) return true;
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return string.Equals(
                Path.GetFullPath(a!).TrimEnd('\\','/'),
                Path.GetFullPath(b!).TrimEnd('\\','/'),
                StringComparison.OrdinalIgnoreCase);
        }

        static string? GetBitmap(Asset a, string name)
        {
            var p = a.FindByName(name);
            if (p == null || p.NumberOfConnectedProperties == 0) return null;
            var sub = p.GetConnectedProperty(0) as Asset;
            var s = sub?.FindByName("unifiedbitmap_Bitmap") as AssetPropertyString;
            return s?.Value;
        }

        public static bool IsNoOp(AppearanceAssetElement appe, MaterRevitAddin.ViewModels.MaterViewModel vm)
        {
            var asset = appe.GetRenderingAsset();
            var diff = vm.GetSlotPath(Models.MapType.DIFF);
            var glos = vm.GetSlotPath(Models.MapType.GLOS);
            var refl = vm.GetSlotPath(Models.MapType.REFL);
            var bump = vm.GetSlotPath(Models.MapType.BUMP);
            var opac = vm.GetSlotPath(Models.MapType.OPAC);

            bool same =
                PathEq(GetBitmap(asset, "generic_diffuse"), diff) &&
                PathEq(GetBitmap(asset, "generic_glossiness"), glos) &&
                PathEq(GetBitmap(asset, "generic_reflectivity_at_90deg"), refl) &&
                PathEq(GetBitmap(asset, "generic_bump"), bump) &&
                PathEq(GetBitmap(asset, "generic_transparency"), opac);

            return same;
        }
    }
}
