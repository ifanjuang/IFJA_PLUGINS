using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace MaterRevitAddin.Utils
{
    public static class FolderService
    {
        public static bool IsImage(string path)
        {
            var e = Path.GetExtension(path).ToLowerInvariant();
            return e == ".jpg" || e == ".jpeg" || e == ".png" || e == ".bmp" || e == ".tif" || e == ".tiff";
        }

        public static void CopyFilesToFolder(IEnumerable<string> files, string targetFolder, bool overwrite = false)
        {
            Directory.CreateDirectory(targetFolder);
            foreach (var src in files)
            {
                if (!File.Exists(src)) continue;
                var dst = Path.Combine(targetFolder, Path.GetFileName(src));
                File.Copy(src, dst, overwrite);
            }
        }

        public static void MoveFilesToFolder(IEnumerable<string> files, string targetFolder, bool overwrite = false)
        {
            Directory.CreateDirectory(targetFolder);
            foreach (var src in files)
            {
                if (!File.Exists(src)) continue;
                var dst = Path.Combine(targetFolder, Path.GetFileName(src));
                if (overwrite && File.Exists(dst)) File.Delete(dst);
                File.Move(src, dst);
            }
        }

        public static void CopyDirectoryMerge(string sourceDir, string targetDir, bool overwrite = false)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceDir, file);
                var dst = Path.Combine(targetDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(file, dst, overwrite);
            }
        }

        public static void MoveDirectoryMerge(string sourceDir, string targetDir, bool overwrite = false)
        {
            CopyDirectoryMerge(sourceDir, targetDir, overwrite);
            try { Directory.Delete(sourceDir, true); } catch { }
        }

        public static void ExtractZipToFolder(string zipPath, string targetFolder, bool overwrite = false)
        {
            Directory.CreateDirectory(targetFolder);
            using var z = ZipFile.OpenRead(zipPath);
            foreach (var entry in z.Entries)
            {
                var dst = Path.Combine(targetFolder, entry.FullName);
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(dst);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                entry.ExtractToFile(dst, overwrite);
            }
        }

        public static void IngestPathsToFolder(IEnumerable<string> paths, string targetFolder, bool move = false, bool overwrite = true)
        {
            Directory.CreateDirectory(targetFolder);
            foreach (var p in paths)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (File.Exists(p) && ext == ".zip")
                {
                    ExtractZipToFolder(p, targetFolder, overwrite);
                    if (move) { try { File.Delete(p); } catch { } }
                }
                else if (Directory.Exists(p))
                {
                    if (move) MoveDirectoryMerge(p, targetFolder, overwrite);
                    else CopyDirectoryMerge(p, targetFolder, overwrite);
                }
                else if (File.Exists(p))
                {
                    if (move) MoveFilesToFolder(new[] { p }, targetFolder, overwrite);
                    else CopyFilesToFolder(new[] { p }, targetFolder, overwrite);
                }
            }
        }
    }
}
