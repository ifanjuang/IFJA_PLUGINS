using System.IO;
using System.Text.RegularExpressions;
using Mater2026.Models;

namespace Mater2026.Services
{
    public static class MapDetect
    {
        // Règles ordonnées par priorité
        private static readonly (MapType type, string[] keys, int prio)[] Rules = new[]
        {
            (MapType.Albedo, new[]{
                "albedo","basecolor","base-colo","base_col","base col",
                "diffuse","diff","color","colour","baseclr","albd"}, 3),

            (MapType.Bump, new[]{
                "normalgl","normaldx","normalmap","normal","nrm","nrml","norm","nor",
                "height","disp","displace","parallax","bumpmap","bump","par"}, 2),

            (MapType.Roughness, new[]{ "roughness","rough","rgh","glossiness","smoothness","smooth","gloss","gls" }, 2),

            (MapType.Reflection, new[]{ "specularity","specular","spec","reflectance","reflection","reflect","refl" }, 1),

            (MapType.Metalness, new[]{ "metalness","metallic","metal","met" }, 1),

            (MapType.Refraction, new[]{ "opacity","opac","alpha","mask","transmission","translucency" }, 1),

            (MapType.Illumination, new[]{ "emissive","emission","emit","selfillum","illum" }, 1),
        };

        private static readonly string[] VendorTokens = new[]{
            "udim","1001","1002","1003",
            "1k","2k","4k","8k","16k","1024","2048","4096","8192",
            "ue4","ue5","unity","marmoset",
            "quixel","megascans","arroway","cgaxis","ambientcg","polyhaven","texturehaven","cc0"
        };

        private static string Norm(string path)
        {
            var s = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            s = Regex.Replace(s, @"[ \-_.]", "");
            foreach (var t in VendorTokens) s = s.Replace(t, "");
            s = s.Replace("basecolour", "basecolor");
            return s;
        }

        public static (MapType type, bool invert, string label) Detect(string file)
        {
            var n = Norm(file);

            if (n.Contains("gloss") || n.Contains("smooth")) return (MapType.Roughness, true, "Glossiness");
            if (n.Contains("rough")) return (MapType.Roughness, false, "Roughness");

            if (n.Contains("normal")) return (MapType.Bump, false, "Normal");
            if (n.Contains("height") || n.Contains("disp") || n.Contains("parallax")) return (MapType.Bump, false, "Displacement");
            if (n.Contains("bump")) return (MapType.Bump, false, "Bump");

            (MapType t, int pr, string key) best = (MapType.Unknown, -1, "");
            foreach (var (type, keys, prio) in Rules)
                foreach (var k in keys)
                    if (n.Contains(k) && prio > best.pr) best = (type, prio, k);

            if (best.t != MapType.Unknown)
                return best.t switch
                {
                    MapType.Albedo => (best.t, false, "Diffuse/Albedo"),
                    MapType.Reflection => (best.t, false, best.key.Contains("gloss") ? "Glossiness" : "Specular/Reflection"),
                    MapType.Metalness => (best.t, false, "Metalness"),
                    MapType.Refraction => (best.t, false, "Opacity/Alpha"),
                    MapType.Illumination => (best.t, false, "Emissive"),
                    _ => (best.t, false, "Detected")
                };

            return (MapType.Unknown, false, "Unknown");
        }
    }
}
