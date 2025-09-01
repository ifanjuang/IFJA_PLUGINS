using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mater2026.Services
{
    public static class FileService
    {
        // Supported source image extensions (case-insensitive)
        public static readonly string[] ImgExt = [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".webp"];

        public static bool IsImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var ext = Path.GetExtension(path);
            return ImgExt.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }
        public static bool HasKeyImage(string folder)
        {
            var name = GetLeafName(folder);
            foreach (var ext in ImgExt)
            {
                var candidate = Path.Combine(folder, name + ext);
                if (File.Exists(candidate)) return true;
            }
            return false;
        }
        public static IEnumerable<string> EnumerateOriginals(string folder)
        {
            if (!Directory.Exists(folder)) yield break;

            foreach (var f in Directory.EnumerateFiles(folder))
            {
                var ext = Path.GetExtension(f);
                if (!ImgExt.Contains(ext)) continue;

                var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                if (name.EndsWith("_128") || name.EndsWith("_512") || name.EndsWith("_1024")) continue;
                if (name.Contains("thumb")) continue;

                yield return f;
            }
        }
        public static string? ResolveThumbSource(string folder)
        {
            var name = GetLeafName(folder);

            // Prefer exact "key image" in any supported ext
            foreach (var ext in ImgExt)
            {
                var exact = Path.Combine(folder, name + ext);
                if (File.Exists(exact)) return exact;
            }

            // Fallback to existing resized thumbs (generated as jpg)
            foreach (var size in new[] { 1024, 512, 128 })
            {
                var p = GetSizedThumbPath(folder, size);
                if (File.Exists(p)) return p;
            }

            return null;
        }

        /// <summary>
        /// Build the expected resized-thumbnail path, always jpg:
        /// folder\{folderName}_{size}.jpg
        /// </summary>
        public static string GetSizedThumbPath(string folder, int size)
        {
            var name = GetLeafName(folder);
            return Path.Combine(folder, $"{name}_{size}.jpg");
        }

        /// <summary>
        /// Check if a _{size}.jpg exists for the folder.
        /// </summary>
        public static bool HasSizedThumb(string folder, int size)
            => File.Exists(GetSizedThumbPath(folder, size));

        /// <summary>
        /// Safe leaf-name from a folder path (handles trailing slashes).
        /// </summary>
        private static string GetLeafName(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return string.Empty;
            folder = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(folder);
        }
    }
}
