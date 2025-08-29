using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mater2026.Services
{
    public static class FileService
    {
        // Supported source image extensions (case-insensitive)
        private static readonly HashSet<string> ImgExt = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".webp"
        };

        /// <summary>
        /// A "key image" is an image whose filename matches the folder name (any supported extension).
        /// e.g. MyFolder\MyFolder.jpg
        /// </summary>
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

        /// <summary>
        /// Enumerate "original" images only:
        /// - must be a supported extension
        /// - exclude resized *_128/_512/_1024
        /// - exclude files containing "thumb" in the name
        /// </summary>
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

        /// <summary>
        /// Return the best source image to base a thumbnail on:
        /// 1) exact key image (folderName.{ext})
        /// 2) else the largest existing resized thumb: _1024 → _512 → _128 (jpg)
        /// </summary>
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
