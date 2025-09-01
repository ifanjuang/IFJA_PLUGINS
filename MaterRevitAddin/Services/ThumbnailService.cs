using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace Mater2026.Services
{
    public static class ThumbnailService
    {
        public static string GenerateThumb(string folder, int size, bool overwrite)
        {
            var name = Path.GetFileName(folder);
            var target = Path.Combine(folder, $"{name}_{size}.jpg");

            var src = FileService.ResolveThumbSource(folder);
            if (src == null) return target;

            bool outdated = File.Exists(target) &&
                            File.GetLastWriteTimeUtc(target) < File.GetLastWriteTimeUtc(src);
            bool needGen = overwrite || !File.Exists(target) || outdated;
            if (!needGen) return target;

            try
            {
                var abs = Path.GetFullPath(src);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(abs, UriKind.Absolute); // correct overload
                if (size > 0) bmp.DecodePixelWidth = size;
                bmp.EndInit();
                bmp.Freeze();

                var enc = new JpegBitmapEncoder { QualityLevel = 88 };
                enc.Frames.Add(BitmapFrame.Create(bmp));

                using var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read);
                enc.Save(fs);
            }
            catch
            {
                // Swallow; callers refresh UI based on what exists.
            }

            return target;
        }
        public static void SetFolderThumbnailFromImage(string folder, string imagePath)
        {
            if (!Directory.Exists(folder) || !File.Exists(imagePath)) return;
            var ext = Path.GetExtension(imagePath);
            var name = Path.GetFileName(folder);
            var target = Path.Combine(folder, name + ext);

            File.Copy(imagePath, target, overwrite: true);
            foreach (var s in new[] { 128, 512, 1024 })
                GenerateThumb(folder, s, overwrite: true);
        }

    }
}
