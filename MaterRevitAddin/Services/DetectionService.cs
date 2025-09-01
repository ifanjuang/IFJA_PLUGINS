using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mater2026.Models;

namespace Mater2026.Services
{
    public static class DetectionService
    {
        public static List<MapFile> ScanAndClassify(string folder)
        {
            var list = new List<MapFile>();
            foreach (var f in FileService.EnumerateOriginals(folder))
            {
                var (type, invert, label) = MapDetect.Detect(f);
                LogService.Info($"Detect: '{Path.GetFileName(f)}' -> {type} (invert={invert}) [{label}]");
                list.Add(new MapFile(f, type));
            }
            return list;
        }

        public static List<MapSlot> DetectSlots(string folder)
        {
            var maps = ScanAndClassify(folder);

            // Cache MapDetect result per file (type/invert/label)
            var meta = maps.ToDictionary(m => m.FullPath, m => MapDetect.Detect(m.FullPath));

            // Helper to get the label quickly
            static string LabelOf((MapType type, bool invert, string label) t) => t.label ?? string.Empty;

            var result = new List<MapSlot>
            {
                new(MapType.Albedo),
                new(MapType.Roughness),
                new(MapType.Reflection),
                new(MapType.Bump),
                new(MapType.Refraction),
                new(MapType.Illumination),
            };

            // ---- BUMP FAMILY: choose Normal > Displacement(Height/Depth) > Bump
            var bumpCandidates = maps.Where(m => m.Type == MapType.Bump).ToList();

            MapFile? bumpPick = bumpCandidates
                .FirstOrDefault(m => LabelOf(meta[m.FullPath]).Equals("Normal", System.StringComparison.OrdinalIgnoreCase))
                ?? bumpCandidates.FirstOrDefault(m => LabelOf(meta[m.FullPath]).Equals("Displacement", System.StringComparison.OrdinalIgnoreCase))
                ?? bumpCandidates.FirstOrDefault();

            string? bumpDetail = null;
            if (bumpPick != null)
            {
                var lbl = LabelOf(meta[bumpPick.FullPath]);
                bumpDetail = lbl.Equals("Normal", System.StringComparison.OrdinalIgnoreCase) ? "Normal"
                           : lbl.Equals("Displacement", System.StringComparison.OrdinalIgnoreCase) ? "Depth"
                           : "Bump";
            }

            // ---- ROUGH/GLOSS: prefer Glossiness (invert=true) else Roughness (invert=false)
            var roughFamily = maps.Where(m => m.Type == MapType.Roughness).ToList();

            MapFile? glossPick = roughFamily
                .FirstOrDefault(m => LabelOf(meta[m.FullPath]).Equals("Glossiness", System.StringComparison.OrdinalIgnoreCase));

            MapFile? roughPick = glossPick == null
                ? roughFamily.FirstOrDefault(m => LabelOf(meta[m.FullPath]).Equals("Roughness", System.StringComparison.OrdinalIgnoreCase))
                : null;

            foreach (var s in result)
            {
                switch (s.Type)
                {
                    case MapType.Albedo:
                        s.Assigned = maps.FirstOrDefault(m => m.Type == MapType.Albedo);
                        break;

                    case MapType.Roughness:
                        if (glossPick != null)
                        {
                            s.Assigned = new MapFile(glossPick.FullPath, MapType.Roughness);
                            s.Invert = true;  // Glossiness -> invert at apply time
                        }
                        else if (roughPick != null)
                        {
                            s.Assigned = new MapFile(roughPick.FullPath, MapType.Roughness);
                            s.Invert = false;
                        }
                        break;

                    case MapType.Reflection:
                        s.Assigned = maps.FirstOrDefault(m => m.Type == MapType.Reflection);
                        break;

                    case MapType.Refraction:
                        s.Assigned = maps.FirstOrDefault(m => m.Type == MapType.Refraction);
                        break;

                    case MapType.Illumination:
                        s.Assigned = maps.FirstOrDefault(m => m.Type == MapType.Illumination);
                        break;

                    case MapType.Bump:
                        if (bumpPick != null)
                        {
                            s.Assigned = new MapFile(bumpPick.FullPath, MapType.Bump);
                            s.Detail = bumpDetail; // "Normal" | "Depth" | "Bump" drives UI & advanced toggle
                        }
                        break;
                }

                LogService.Info(
                    $"Slot {s.Type}: {(s.Assigned?.FileName ?? "—")} invert={s.Invert} detail={s.Detail}");
            }

            return result;
        }
    }
}
