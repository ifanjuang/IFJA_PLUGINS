using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Mater2026.Models;

namespace Mater2026.Services
{
    public static partial class RevitMaterialService
    {
        [GeneratedRegex(@"tiles_(\d+)_\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex TilesRegex();

        public static IList<(ElementId Id, string Name)> GetProjectMaterials(Autodesk.Revit.UI.UIDocument uidoc)
        {
            var doc = uidoc.Document;
            var mats = new FilteredElementCollector(doc)
                .OfClass(typeof(Material)).Cast<Material>()
                .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase);

            return mats.Select(m => (m.Id, m.Name)).ToList();
        }

        public static (Material? Mat, AppearanceAssetElement? App) GetMaterialAndAppearance(Document doc, ElementId materialId)
        {
            var mat = doc.GetElement(materialId) as Material;
            AppearanceAssetElement? app = null;
            if (mat != null && mat.AppearanceAssetId != ElementId.InvalidElementId)
                app = doc.GetElement(mat.AppearanceAssetId) as AppearanceAssetElement;
            return (mat, app);
        }

        public static AppearanceAssetElement GetOrCreateAppearanceByFolder(Document doc, string folderName)
        {
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>()
                .FirstOrDefault(a => a.Name.Equals(folderName, StringComparison.CurrentCultureIgnoreCase));
            if (existing != null) return existing;

            var baseApp =
                new FilteredElementCollector(doc)
                    .OfClass(typeof(AppearanceAssetElement))
                    .Cast<AppearanceAssetElement>()
                    .FirstOrDefault(a => a.Name.Equals("Générique", StringComparison.CurrentCultureIgnoreCase))
                ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(AppearanceAssetElement))
                    .Cast<AppearanceAssetElement>()
                    .FirstOrDefault(a => a.Name.Equals("Generic", StringComparison.CurrentCultureIgnoreCase))
                ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(AppearanceAssetElement))
                    .Cast<AppearanceAssetElement>()
                    .First();

            using var tx = new Transaction(doc, "Create Appearance");
            tx.Start();
            var dup = baseApp!.Duplicate(folderName);
            tx.Commit();
            return dup;
        }

        public static void WriteAppearanceFolderPath(AppearanceAssetElement app, string folderPath)
        {
            var doc = app.Document;
            using var tx = new Transaction(doc, "Set Appearance Description");
            tx.Start();
            using (var scope = new AppearanceAssetEditScope(doc))
            {
                var editable = scope.Start(app.Id);
                // Description lives on SchemaCommon.Description (Visual namespace)
                var desc = editable.FindByName(SchemaCommon.Description) as AssetPropertyString;
                if (desc != null && !desc.IsReadOnly)
                    desc.Value = folderPath ?? string.Empty;
                scope.Commit(true);
            }
            tx.Commit();
        }

        public static string ReadAppearanceFolderPath(AppearanceAssetElement app)
        {
            var asset = app.GetRenderingAsset();
            var desc = asset.FindByName(SchemaCommon.Description) as AssetPropertyString;
            return desc?.Value ?? string.Empty;
        }

        private static Asset? GetUBFromSlot(Asset root, string slotKeyword)
        {
            var prop = root.FindByName(slotKeyword) as AssetProperty;
            return prop?.GetSingleConnectedAsset();
        }

        private static T? FindProp<T>(Asset a, params string[] names) where T : AssetProperty
        {
            foreach (var n in names)
                if (a.FindByName(n) is T p) return p;
            return null;
        }

        private static void SetUBCore(Asset ub, string? filePath, bool? invert, double? widthCm, double? heightCm, double? rotationDeg)
        {
            var pBitmap = FindProp<AssetPropertyString>(ub, "unifiedbitmap_Bitmap", "UnifiedBitmap.Bitmap");
            var pInvert = FindProp<AssetPropertyBoolean>(ub, "unifiedbitmap_Invert", "UnifiedBitmap.Invert");
            var pSX = FindProp<AssetPropertyDouble>(ub, "unifiedbitmap_RealWorldScaleX", "UnifiedBitmap.RealWorldScaleX", "texture_RealWorldScaleX");
            var pSY = FindProp<AssetPropertyDouble>(ub, "unifiedbitmap_RealWorldScaleY", "UnifiedBitmap.RealWorldScaleY", "texture_RealWorldScaleY");
            var pRot = FindProp<AssetPropertyDouble>(ub, "UnifiedBitmap.Rotation", "unifiedbitmap_WAngle", "texture_Rotation", "texture_WAngle");

            if (filePath != null && pBitmap != null && !pBitmap.IsReadOnly) pBitmap.Value = filePath;
            if (invert.HasValue && pInvert != null && !pInvert.IsReadOnly) pInvert.Value = invert.Value;

            if (widthCm.HasValue && pSX != null && !pSX.IsReadOnly) pSX.Value = widthCm.Value / 100.0;
            if (heightCm.HasValue && pSY != null && !pSY.IsReadOnly) pSY.Value = heightCm.Value / 100.0;
            if (rotationDeg.HasValue && pRot != null && !pRot.IsReadOnly) pRot.Value = rotationDeg.Value;
        }

        private static void SetUBOverlayTint(Asset ub, (int r, int g, int b)? tint)
        {
            if (!tint.HasValue) return;
            var (r, g, b) = tint.Value;

            var pToggle = FindProp<AssetPropertyBoolean>(ub, "common_Tint_toggle", "UnifiedBitmap.Tint_enabled", "unifiedbitmap_Tint_toggle");
            var pTintColor = FindProp<AssetPropertyDoubleArray4d>(ub, "common_Tint_color", "UnifiedBitmap.Tint_color", "unifiedbitmap_Tint_color", "TintColor");

            if (pToggle != null && !pToggle.IsReadOnly) pToggle.Value = true;
            if (pTintColor != null && !pTintColor.IsReadOnly)
            {
                double r01 = Math.Clamp(r / 255.0, 0, 1);
                double g01 = Math.Clamp(g / 255.0, 0, 1);
                double b01 = Math.Clamp(b / 255.0, 0, 1);
                pTintColor.SetValueAsDoubles(new List<double> { r01, g01, b01, 1.0 });
            }
        }

        private static (string? path, bool invert, (int r, int g, int b)? tint, double? sx, double? sy, double? rot)
            ReadUBAll(Asset ub)
        {
            var pBitmap = FindProp<AssetPropertyString>(ub, "unifiedbitmap_Bitmap", "UnifiedBitmap.Bitmap");
            var pInvert = FindProp<AssetPropertyBoolean>(ub, "unifiedbitmap_Invert", "UnifiedBitmap.Invert");
            var pSX = FindProp<AssetPropertyDouble>(ub, "unifiedbitmap_RealWorldScaleX", "UnifiedBitmap.RealWorldScaleX", "texture_RealWorldScaleX");
            var pSY = FindProp<AssetPropertyDouble>(ub, "unifiedbitmap_RealWorldScaleY", "UnifiedBitmap.RealWorldScaleY", "texture_RealWorldScaleY");
            var pRot = FindProp<AssetPropertyDouble>(ub, "UnifiedBitmap.Rotation", "unifiedbitmap_WAngle", "texture_Rotation", "texture_WAngle");

            var pTintToggle = FindProp<AssetPropertyBoolean>(ub, "common_Tint_toggle", "UnifiedBitmap.Tint_enabled", "unifiedbitmap_Tint_toggle");
            var pTintColor = FindProp<AssetPropertyDoubleArray4d>(ub, "common_Tint_color", "UnifiedBitmap.Tint_color", "unifiedbitmap_Tint_color", "TintColor");

            (int r, int g, int b)? tint = null;
            if (pTintToggle?.Value == true && pTintColor != null)
            {
                var v = pTintColor.GetValueAsDoubles();
                if (v != null && v.Count >= 3)
                {
                    int rr = (int)Math.Round(v[0] * 255.0);
                    int gg = (int)Math.Round(v[1] * 255.0);
                    int bb = (int)Math.Round(v[2] * 255.0);
                    tint = (rr, gg, bb);
                }
            }

            return (pBitmap?.Value, pInvert?.Value ?? false, tint, pSX?.Value, pSY?.Value, pRot?.Value);
        }

        public static void ApplyUiToAppearance(
            AppearanceAssetElement app,
            Dictionary<MapType, (string? path, bool invert)> maps,
            double widthCm, double heightCm, double rotationDeg, (int r, int g, int b)? tint)
        {
            var doc = app.Document;
            using var tx = new Transaction(doc, "Apply Appearance");
            tx.Start();

            using (var scope = new AppearanceAssetEditScope(doc))
            {
                var editable = scope.Start(app.Id);

                void SetSlot(string slot, MapType type, bool allowInvert = false, bool setTint = false)
                {
                    var ub = GetUBFromSlot(editable, slot);
                    if (ub == null) return;

                    maps.TryGetValue(type, out var val);
                    var invert = allowInvert && val.invert;
                    SetUBCore(ub, val.path, invert, widthCm, heightCm, rotationDeg);
                    if (setTint) SetUBOverlayTint(ub, tint);
                }

                SetSlot("generic_diffuse", MapType.Albedo, false, true);
                SetSlot("generic_glossiness", MapType.Roughness, true);
                SetSlot("generic_roughness", MapType.Roughness, true);
                SetSlot("generic_bump_map", MapType.Bump);
                SetSlot("generic_reflectivity_at_0deg", MapType.Reflection);
                SetSlot("generic_transparency", MapType.Refraction);
                SetSlot("generic_emission_color", MapType.Illumination);

                scope.Commit(true);
            }

            tx.Commit();
        }

        public static UiReadback ReadUiFromAppearanceAndMaterial(Document doc, ElementId materialId)
        {
            var rb = new UiReadback();
            var (mat, app) = GetMaterialAndAppearance(doc, materialId);
            if (app == null) return rb;

            var asset = app.GetRenderingAsset();
            rb.FolderPath = ReadAppearanceFolderPath(app);

            (string? path, bool inv, (int r, int g, int b)? tint, double? sx, double? sy, double? rot) Probe(string slot)
            {
                var ub = GetUBFromSlot(asset, slot);
                if (ub == null) return (null, false, null, null, null, null);
                return ReadUBAll(ub);
            }

            var albedo = Probe("generic_diffuse");
            var gloss = Probe("generic_glossiness");
            var rough = Probe("generic_roughness");
            var bump = Probe("generic_bump_map");
            var refl = Probe("generic_reflectivity_at_0deg");
            var refr = Probe("generic_transparency");
            var emis = Probe("generic_emission_color");

            if (albedo.path != null) rb.Maps[MapType.Albedo] = (albedo.path, false);
            if (gloss.path != null) rb.Maps[MapType.Roughness] = (gloss.path, true);
            else if (rough.path != null) rb.Maps[MapType.Roughness] = (rough.path, rough.inv);

            if (bump.path != null) rb.Maps[MapType.Bump] = (bump.path, false);
            if (refl.path != null) rb.Maps[MapType.Reflection] = (refl.path, false);
            if (refr.path != null) rb.Maps[MapType.Refraction] = (refr.path, false);
            if (emis.path != null) rb.Maps[MapType.Illumination] = (emis.path, false);

            rb.Tint = albedo.tint;

            double? sxm = albedo.sx ?? rough.sx ?? bump.sx;
            double? sym = albedo.sy ?? rough.sy ?? bump.sy;
            double? rot = albedo.rot ?? rough.rot ?? bump.rot;
            if (sxm.HasValue) rb.WidthCm = sxm.Value * 100.0;
            if (sym.HasValue) rb.HeightCm = sym.Value * 100.0;
            if (rot.HasValue) rb.RotationDeg = rot.Value;

            try
            {
                if (mat != null && mat.SurfaceForegroundPatternId != ElementId.InvalidElementId)
                {
                    if (doc.GetElement(mat.SurfaceForegroundPatternId) is FillPatternElement fpe)
                    {
                        var name = fpe.Name ?? "";
                        var m = TilesRegex().Match(name);
                        if (m.Success)
                        {
                            if (int.TryParse(m.Groups[1].Value, out var tx)) rb.TilesX = tx;
                            if (int.TryParse(m.Groups[2].Value, out var ty)) rb.TilesY = ty;
                        }
                    }
                }
            }
            catch { }

            return rb;
        }

        public static Material CreateMaterial(Document doc, string materialName, AppearanceAssetElement app)
        {
            using var tx = new Transaction(doc, "Create Material");
            tx.Start();
            var mid = Material.Create(doc, materialName);
            var mat = (Material)doc.GetElement(mid);
            mat.AppearanceAssetId = app.Id;
            tx.Commit();
            return mat;
        }

        public static void ReplaceMaterialAppearance(Material mat, AppearanceAssetElement app)
        {
            using var tx = new Transaction(mat.Document, "Replace Material Appearance");
            tx.Start();
            mat.AppearanceAssetId = app.Id;
            tx.Commit();
        }
    }
}
