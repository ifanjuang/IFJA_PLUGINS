using System;
using System.IO;
using Mater2026.Models;

namespace Mater2026.Services
{
    public static class MapDetect
    {
        static readonly string[] DIFF = { "basecolour", "basecolor", "albedo", "diffuse", "colour", "color", "rgb", "clr", "col", "diff" };
        static readonly string[] NORM = { "normalgl", "normaldx", "normalmap", "normal", "nrm", "nrml", "norm", "nor" };
        static readonly string[] DISP = { "displacement", "displace", "parallax", "height", "depth", "disp", "hgt", "hght" };
        static readonly string[] BUMP = { "bumpmap", "bump", "bmp" };
        static readonly string[] GLOS = { "glossiness", "smoothness", "smooth", "gloss", "gls" };
        static readonly string[] ROUG = { "roughness", "rough", "rgh" };
        static readonly string[] REFL = { "specularity", "specular", "reflectance", "reflection", "reflect", "refl", "spec", "ref" };
        static readonly string[] METL = { "metalness", "metallic", "metal", "met" };
        static readonly string[] OPAC = { "opacitymask", "alphamasked", "transparency", "translucency", "translucent", "alpha", "mask", "cutout", "opacity", "opac", "opa" };
        static readonly string[] AO = { "ambientocclusion", "occ", "ao" };
        static readonly string[] EMIS = { "incandescence", "luminance", "selfillumination", "selfillum", "emission", "emissive", "emit", "glow" };

        static string Norm(string name)
        {
            var s = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
            Span<char> buf = stackalloc char[s.Length];
            int j = 0;
            foreach (var c in s)
                if (c != ' ' && c != '-' && c != '_' && c != '.') buf[j++] = c;
            s = new string(buf.Slice(0, j));
            s = s.Replace("1k", "").Replace("2k", "").Replace("4k", "").Replace("8k", "")
                 .Replace("1024", "").Replace("2048", "").Replace("4096", "").Replace("8192", "");
            return s;
        }
        static bool EndsWithAny(string s, string[] arr)
        {
            foreach (var a in arr) if (s.EndsWith(a, StringComparison.Ordinal)) return true;
            return false;
        }

        // Returns (type, invert, label)
        public static (MapType type, bool invert, string label) Detect(string file)
        {
            var n = Norm(file);

            if (EndsWithAny(n, ROUG)) return (MapType.Roughness, true, "Roughness (invert)");
            if (EndsWithAny(n, GLOS)) return (MapType.Roughness, false, "Glossiness");

            if (EndsWithAny(n, NORM)) return (MapType.Bump, false, "Normal");
            if (EndsWithAny(n, DISP)) return (MapType.Bump, false, "Displacement/Height");
            if (EndsWithAny(n, BUMP)) return (MapType.Bump, false, "Bump");

            if (EndsWithAny(n, REFL)) return (MapType.Reflection, false, "Specular/Reflect");
            if (EndsWithAny(n, METL)) return (MapType.Reflection, false, "Metalness (fallback)");

            if (EndsWithAny(n, OPAC)) return (MapType.Refraction, false, "Opacity/Alpha");
            if (EndsWithAny(n, DIFF)) return (MapType.Albedo, false, "Diffuse/Albedo");

            if (EndsWithAny(n, AO)) return (MapType.Unknown, false, "AO (info)");
            if (EndsWithAny(n, EMIS)) return (MapType.Illumination, false, "Emissive");

            return (MapType.Unknown, false, "Unknown");
        }
    }
}
