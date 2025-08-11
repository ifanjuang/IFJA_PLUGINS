using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;

namespace MaterRevitAddin.Utils
{
    public static class AssetUtils
    {
        public static double MetersToInches(double m) => m / 0.0254;
        public static double MetersToFeet(double m) => m / 0.3048;

        public static void EnsureUnifiedBitmap(Asset asset, string propName, out Asset? ub)
        {
            ub = null;
            var p = asset.FindByName(propName) as AssetProperty;
            if (p == null) return;

            if (p.NumberOfConnectedProperties == 0)
            {
                var kid = asset.AddConnectedAsset(propName, "UnifiedBitmap");
                ub = kid as Asset;
            }
            else
            {
                ub = p.GetConnectedProperty(0) as Asset;
            }
        }

        public static void SetBitmapProperty(Asset asset, string propName, string? filePath,
            double scaleX_m, double scaleY_m, double rotation_deg, bool invertRoughness = false)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            EnsureUnifiedBitmap(asset, propName, out var ub);
            if (ub == null) return;

            if (ub.FindByName("unifiedbitmap_Bitmap") is AssetPropertyString pathProp) pathProp.Value = filePath;
            if (ub.FindByName("unifiedbitmap_realworldsizex") is AssetPropertyDouble sx) sx.Value = MetersToInches(scaleX_m);
            if (ub.FindByName("unifiedbitmap_realworldsizey") is AssetPropertyDouble sy) sy.Value = MetersToInches(scaleY_m);
            if (ub.FindByName("unifiedbitmap_rotation") is AssetPropertyDouble rot) rot.Value = rotation_deg;

            if (ub.FindByName("texture_Invert") is AssetPropertyBoolean inv) inv.Value = invertRoughness;
        }

        public static void SetEnum(Asset asset, string propName, string enumValue)
        {
            if (asset.FindByName(propName) is AssetPropertyEnum ap)
            {
                for (int i = 0; i < ap.Names.Size; ++i)
                {
                    if (ap.Names[i] == enumValue) { ap.Value = i; break; }
                }
            }
        }
    }
}
