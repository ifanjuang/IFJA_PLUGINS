using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MaterRevitAddin.Models;

namespace MaterRevitAddin.Utils
{
    public enum VendorProfile { Generic, Substance, Quixel, PolyHaven }

    public static class MapFileUtils
    {
        static readonly Dictionary<string, string[]> dict = new(StringComparer.OrdinalIgnoreCase)
        {
            { "diffuse", new[] { "diff", "diffuse", "_color", "col", "clr", "albedo", "alb", "rgb", "basecolor", "base_color" } },
            { "bump_map", new[] { "normal", "normaldx", "normalgl", "nrm", "norm", "nor", "height", "bump_map", "bumpmap", "bump", "bmp", "hght", "hgt", "displacement", "displace", "disp", "depth" } },
            { "glossiness", new[] { "glossiness", "gloss", "smoothness", "smooth", "g" } },
            { "roughness", new[] { "roughness", "rough", "rgh" } },
            { "reflectivity_at_90deg", new[] { "reflection", "reflect", "refl", "ref", "specularity", "specular", "spec", "s", "metallic", "metalness", "metal" } },
            { "opacity", new[] { "opacity", "opac", "alphamasked", "alpha", "transparency" } }
        };

        static readonly string[] exts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };

        public static IEnumerable<string> EnumImages(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) yield break;
            foreach (var p in Directory.EnumerateFiles(folder))
                if (exts.Contains(Path.GetExtension(p).ToLowerInvariant()))
                    yield return p;
        }

        public static int? DetectLod(string name)
        {
            var m = Regex.Match(name, @"(?i)(\b[1248]k\b)");
            if (!m.Success) return null;
            return int.TryParse(m.Groups[1].Value[..^1], out int lod) ? lod : null;
        }

        public static (double? x_m, double? y_m) DetectSizeFromName(string name)
        {
            name = name.ToLowerInvariant();
            var mmxy = Regex.Match(name, @"(\d{2,5})x(\d{2,5})\s*mm");
            if (mmxy.Success) return (int.Parse(mmxy.Groups[1].Value)/1000.0, int.Parse(mmxy.Groups[2].Value)/1000.0);

            var mm = Regex.Match(name, @"(\d{2,5})\s*mm");
            if (mm.Success) { var v = int.Parse(mm.Groups[1].Value)/1000.0; return (v, v); }

            var mxy = Regex.Match(name, @"(\d+(?:\.\d+)?)x(\d+(?:\.\d+)?)\s*m");
            if (mxy.Success) return (double.Parse(mxy.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                                     double.Parse(mxy.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));

            var ms = Regex.Match(name, @"(\d+(?:\.\d+)?)\s*m");
            if (ms.Success) { var v = double.Parse(ms.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture); return (v, v); }

            return (null, null);
        }

        public static (MapType slot, string label, string icon) Detect(string filePath, VendorProfile profile = VendorProfile.Generic)
        {
            var name = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();

            if (dict["glossiness"].Any(k => name.Contains(k))) return (MapType.GLOS, "Glossiness", "Resources/Icons/gloss.png");
            if (dict["roughness"].Any(k => name.Contains(k))) return (MapType.GLOS, "Roughness (inv)", "Resources/Icons/rough.png");

            if (name.Contains("normal")) return (MapType.BUMP, "Normal", "Resources/Icons/normal.png");
            if (name.Contains("depth") || name.Contains("disp") || name.Contains("height")) return (MapType.BUMP, "Depth", "Resources/Icons/depth.png");
            if (name.Contains("bump")) return (MapType.BUMP, "Bump", "Resources/Icons/bump.png");

            if (dict["diffuse"].Any(k => name.Contains(k))) return (MapType.DIFF, "Diffuse", "Resources/Icons/diffuse.png");

            if (dict["reflectivity_at_90deg"].Any(k => name.Contains(k))) return (MapType.REFL, "Reflectivity (90°)", "Resources/Icons/reflect.png");

            if (dict["opacity"].Any(k => name.Contains(k))) return (MapType.OPAC, "Opacity", "Resources/Icons/opacity.png");

            return (MapType.NONE, "Unknown", "Resources/Icons/unknown.png");
        }
    }
}
