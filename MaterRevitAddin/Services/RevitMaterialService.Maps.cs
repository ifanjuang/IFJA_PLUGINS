using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Mater2026.Models;

namespace Mater2026.Services
{
    public static partial class RevitMaterialService
    {

        public static void ApplyMapsToMaterial(
        Document doc,
        Material mat,
        IDictionary<MapType, (string? path, bool invert, string? detail)> maps,
        double widthCm, double heightCm, double rotationDeg,
        (int r, int g, int b)? tint)
        {
            EnsureGenericAppearance(doc, mat);

            // If ApplyUiToMaterial expects cm (your version does), keep as-is.
            // If it expects feet, convert here using Units.CmToFt.
            ApplyUiToMaterial(doc, mat, maps, widthCm, heightCm, rotationDeg, tint);
            // ApplyUiToMaterial(doc, mat, maps, Units.CmToFt(widthCm), Units.CmToFt(heightCm), rotationDeg, tint);
        }
        // --- Regex tuiles : "tile_0_1", "tiles-2x3", "tile 1 0" ---
        private static readonly Regex _tilesRx = new(@"tile?s?[-_ ]*(\d+)\D+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static (int tx, int ty)? TryParseTiles(string name)
        {
            var m = _tilesRx.Match(name ?? "");
            if (!m.Success) return null;
            return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
        }

        // --- Utils ---
        public static double CmToFeet(double cm) => cm / 30.48;

        private static T? FindProp<T>(Asset asset, params string[] names) where T : AssetProperty
        {
            foreach (var n in names)
            {
                if (asset.FindByName(n) is T p) return p;
            }
            return null;
        }
        private static Asset? FindSubAsset(Asset parent, params string[] names)
        {
            foreach (var n in names)
                if (parent.FindByName(n) is Asset a) return a;
            return null;
        }
        private static void EnableTexture(Asset host, string onName)
        {
            var on = FindProp<AssetPropertyBoolean>(host, onName);
            if (on != null && !on.IsReadOnly) on.Value = true;
        }

        private static void SetUnifiedBitmap(Asset ub, string? path, bool invert, double? sxFeet, double? syFeet, double? rotDeg)
        {
            var pBitmap = FindProp<AssetPropertyString>(ub, "UnifiedBitmap.Bitmap", "unifiedbitmap_Bitmap", "texture_Bitmap");
            if (pBitmap != null && !pBitmap.IsReadOnly && !string.IsNullOrWhiteSpace(path)) pBitmap.Value = path;

            var pInvert = FindProp<AssetPropertyBoolean>(ub, "UnifiedBitmap.Invert", "unifiedbitmap_Invert");
            if (pInvert != null && !pInvert.IsReadOnly) pInvert.Value = invert;

            var pSX = FindProp<AssetPropertyDouble>(ub, "UnifiedBitmap.RealWorldScaleX", "unifiedbitmap_RealWorldScaleX", "texture_RealWorldScaleX");
            var pSY = FindProp<AssetPropertyDouble>(ub, "UnifiedBitmap.RealWorldScaleY", "unifiedbitmap_RealWorldScaleY", "texture_RealWorldScaleY");
            if (pSX != null && !pSX.IsReadOnly && sxFeet.HasValue) pSX.Value = sxFeet.Value;
            if (pSY != null && !pSY.IsReadOnly && syFeet.HasValue) pSY.Value = syFeet.Value;

            var pRot = FindProp<AssetPropertyDouble>(ub, "UnifiedBitmap.WAngle", "unifiedbitmap_WAngle", "texture_WAngle", "texture_Rotation");
            if (pRot != null && !pRot.IsReadOnly && rotDeg.HasValue) pRot.Value = rotDeg.Value;
        }

        // --- Apparence "générique-like" multilingue : "generi*" (générique, genérico, ...)
        private static string NormalizeKey(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var nf = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(nf.Length);
            foreach (var ch in nf)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
        private static bool StartsWithGeneri(string? s) => NormalizeKey(s).StartsWith("generi");

        /// <summary>
        /// Assure qu'un matériau possède une apparence "générique-like" DUPLIQUÉE et prête à l'édition.
        /// </summary>
        public static ElementId EnsureGenericAppearance(Document doc, Material mat)
        {
            if (mat.AppearanceAssetId != ElementId.InvalidElementId)
            {
                var src = doc.GetElement(mat.AppearanceAssetId) as AppearanceAssetElement;
                if (src != null)
                {
                    using var t = new Transaction(doc, "Duplicate Appearance Asset");
                    t.Start();
                    var dup = src.Duplicate($"{src.Name} (Mater)");
                    mat.AppearanceAssetId = dup.Id;
                    t.Commit();
                    return dup.Id;
                }
            }

            // Cherche "generi*" dans noms d'éléments ou d'assets internes
            var all = new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement)).Cast<AppearanceAssetElement>().ToList();
            AppearanceAssetElement? pick = null;

            foreach (var ae in all)
            {
                try
                {
                    if (StartsWithGeneri(ae.Name)) { pick = ae; break; }
                    var a = ae.GetRenderingAsset();
                    if (a != null && StartsWithGeneri(a.Name)) { pick = ae; break; }
                }
                catch { }
            }

            // Fallback : un asset exposant "generic_diffuse"
            if (pick == null)
            {
                foreach (var ae in all)
                {
                    try
                    {
                        var a = ae.GetRenderingAsset();
                        if (a != null && a.FindByName("generic_diffuse") is AssetProperty) { pick = ae; break; }
                    }
                    catch { }
                }
            }

            // Fallback final
            pick ??= all.FirstOrDefault()
                 ?? throw new InvalidOperationException("Aucun AppearanceAssetElement disponible.");

            using (var t = new Transaction(doc, "Assign Generic-like Appearance"))
            {
                t.Start();
                var dup = pick.Duplicate($"{mat.Name}_Generic");
                mat.AppearanceAssetId = dup.Id;
                t.Commit();
                return dup.Id;
            }
        }

        // --- Application "UI → Matériau" (panneau droit -> maps + tailles/rotation)
        public static void ApplyUiToMaterial(
            Document doc,
            Material mat,
            IDictionary<Models.MapType, (string? path, bool invert, string? detail)> maps,
            double widthCm, double heightCm, double rotationDeg,
            (int r, int g, int b)? tint)
        {
            var appId = EnsureGenericAppearance(doc, mat);

            using var t = new Transaction(doc, "Apply UI Maps to Material");
            t.Start();

            using var scope = new AppearanceAssetEditScope(doc);
            scope.Start(appId);
            var editable = scope.GetRenderingAsset();

            // Diffuse
            ApplyTextureToSlot(editable,
                slotTexNames: new[] { "generic_diffuse_tex", "Generic_Diffuse", "diffuse_tex", "common_Tint_color_texture" },
                slotOnName: "generic_diffuse_on",
                unifiedBitmapNodeName: "unifiedbitmap",
                file: maps.TryGetValue(Models.MapType.Albedo, out var albedo) ? (albedo.path, false) : (null, false),
                sxFeet: CmToFeet(widthCm), syFeet: CmToFeet(heightCm), rotDeg: rotationDeg);

            // Roughness / Gloss (invert si gloss)
            if (maps.TryGetValue(Models.MapType.Roughness, out var rough))
            {
                ApplyTextureToSlot(editable,
                    slotTexNames: new[] { "generic_glossiness_tex", "generic_roughness_tex", "generic_reflect_glossiness_tex" },
                    slotOnName: "generic_glossiness_on",
                    unifiedBitmapNodeName: "unifiedbitmap",
                    file: (rough.path, rough.invert),
                    sxFeet: CmToFeet(widthCm), syFeet: CmToFeet(heightCm), rotDeg: rotationDeg);
            }

            // Spec / Reflection
            if (maps.TryGetValue(Models.MapType.Reflection, out var spec))
            {
                ApplyTextureToSlot(editable,
                    slotTexNames: new[] { "generic_reflectivity_tex", "generic_specular_tex", "generic_reflection_tex" },
                    slotOnName: "generic_reflectivity_on",
                    unifiedBitmapNodeName: "unifiedbitmap",
                    file: (spec.path, false),
                    sxFeet: CmToFeet(widthCm), syFeet: CmToFeet(heightCm), rotDeg: rotationDeg);
            }

            // Metalness (+ fallback reflectivity si slot manquant)
            if (maps.TryGetValue(Models.MapType.Metalness, out var metal))
            {
                var ok = ApplyTextureToSlot(editable,
                    slotTexNames: new[] { "generic_metalness_tex", "pbr_metalness_tex" },
                    slotOnName: "generic_metalness_on",
                    unifiedBitmapNodeName: "unifiedbitmap",
                    file: (metal.path, false),
                    sxFeet: CmToFeet(widthCm), syFeet: CmToFeet(heightCm), rotDeg: rotationDeg,
                    returnWhetherApplied: true);

                if (!ok && !string.IsNullOrWhiteSpace(metal.path))
                {
                    ApplyTextureToSlot(editable,
                        slotTexNames: new[] { "generic_reflectivity_tex" },
                        slotOnName: "generic_reflectivity_on",
                        unifiedBitmapNodeName: "unifiedbitmap",
                        file: (metal.path, false),
                        sxFeet: CmToFeet(widthCm), syFeet: CmToFeet(heightCm), rotDeg: rotationDeg);
                }
            }

            // Bump / Normal
            if (maps.TryGetValue(Models.MapType.Bump, out var bump))
            {
                ApplyTextureToSlot(editable,
                    slotTexNames: new[] { "generic_bump_map", "generic_bump_tex", "generic_normalmap_tex", "generic_normaltex" },
                    slotOnName: "generic_bump_map_on",
                    unifiedBitmapNodeName: "unifiedbitmap",
                    file: (bump.path, false),
                    sxFeet: CmToFeet(widthCm), syFeet: CmToFeet(heightCm), rotDeg: rotationDeg);

                var amt = FindProp<AssetPropertyDouble>(editable, "generic_bump_amount", "bump_amount");
                if (amt != null && !amt.IsReadOnly && amt.Value < 0.5) amt.Value = 0.5;
            }

            // Opacity / Alpha
            if (maps.TryGetValue(Models.MapType.Refraction, out var opac))
            {
                ApplyTextureToSlot(editable,
                    slotTexNames: new[] { "generic_transparency_tex", "generic_opacity_tex", "generic_cutout_tex" },
                    slotOnName: "generic_transparency_on",
                    unifiedBitmapNodeName: "unifiedbitmap",
                    file: (opac.path, false),
                    sxFeet: CmToFeet(widthCm), syFeet: CmToFeet(heightCm), rotDeg: rotationDeg);
            }

            // Emissive
            if (maps.TryGetValue(Models.MapType.Illumination, out var emi))
            {
                ApplyTextureToSlot(editable,
                    slotTexNames: new[] { "generic_emission_tex", "generic_selfillum_tex", "generic_emissive_tex" },
                    slotOnName: "generic_emission_on",
                    unifiedBitmapNodeName: "unifiedbitmap",
                    file: (emi.path, false),
                    sxFeet: CmToFeet(widthCm), syFeet: CmToFeet(heightCm), rotDeg: rotationDeg);
            }

            scope.Commit(true);
            t.Commit();
        }

        private static bool ApplyTextureToSlot(
            Asset editable,
            string[] slotTexNames,
            string slotOnName,
            string unifiedBitmapNodeName,
            (string? path, bool invert) file,
            double? sxFeet, double? syFeet, double? rotDeg,
            bool returnWhetherApplied = false)
        {
            Asset? slotTex = null;
            foreach (var n in slotTexNames)
            {
                slotTex = FindSubAsset(editable, n);
                if (slotTex != null) break;
            }
            if (slotTex == null) return returnWhetherApplied ? false : false;

            EnableTexture(editable, slotOnName);

            var ub = FindSubAsset(slotTex, unifiedBitmapNodeName);
            if (ub == null) return returnWhetherApplied ? false : false;

            if (!string.IsNullOrWhiteSpace(file.path))
                SetUnifiedBitmap(ub, file.path, file.invert, sxFeet, syFeet, rotDeg);

            return true;
        }
    }
}
