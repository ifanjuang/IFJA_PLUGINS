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
            foreach (var f in FileService.EnumerateOriginals(folder)) // FileService now resolves
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

            var result = new List<MapSlot>
            {
                new(MapType.Albedo),
                new(MapType.Roughness),
                new(MapType.Reflection),
                new(MapType.Bump),
                new(MapType.Refraction),
                new(MapType.Illumination)
            };

            // Bump priority: Normal > Disp/Height > Bump
            MapFile? pickBump()
                => maps.FirstOrDefault(m => m.Type == MapType.Bump && MapDetect.Detect(m.FullPath).label.Contains("Normal"))
                ?? maps.FirstOrDefault(m => m.Type == MapType.Bump && MapDetect.Detect(m.FullPath).label.Contains("Displacement"))
                ?? maps.FirstOrDefault(m => m.Type == MapType.Bump);

            // Rough/Gloss: invert when gloss
            MapFile? rough = maps.FirstOrDefault(m => m.Type == MapType.Roughness && !MapDetect.Detect(m.FullPath).label.Contains("Gloss"));
            MapFile? gloss = maps.FirstOrDefault(m => m.Type == MapType.Roughness && MapDetect.Detect(m.FullPath).label.Contains("Gloss"));

            foreach (var s in result)
            {
                switch (s.Type)
                {
                    case MapType.Albedo: s.Assigned = maps.FirstOrDefault(m => m.Type == MapType.Albedo); break;
                    case MapType.Roughness: s.Assigned = gloss ?? rough; s.Invert = gloss != null; break;
                    case MapType.Reflection: s.Assigned = maps.FirstOrDefault(m => m.Type == MapType.Reflection); break;
                    case MapType.Refraction: s.Assigned = maps.FirstOrDefault(m => m.Type == MapType.Refraction); break;
                    case MapType.Illumination: s.Assigned = maps.FirstOrDefault(m => m.Type == MapType.Illumination); break;
                    case MapType.Bump: s.Assigned = pickBump(); break;
                }
                LogService.Info($"Slot {s.Type}: {(s.Assigned?.FileName ?? "—")} invert={s.Invert}");
            }
            return result;
        }
    }
}
