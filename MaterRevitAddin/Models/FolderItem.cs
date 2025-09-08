using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Mater2026.Models
{
    public class FolderItem : INotifyPropertyChanged
    {
        public string FullPath { get; set; } = "";

        public string Name
            => Path.GetFileName(
                FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        private int _thumbSize = 512;
        public int ThumbSize
        {
            get => _thumbSize;
            set
            {
                if (_thumbSize == value) return;
                _thumbSize = value;
                TouchThumb();                      // refresh when size changes
                OnPropertyChanged(nameof(ThumbSize));
            }
        }

        private string? _thumbPath;
        public string? ThumbPath
        {
            get => _thumbPath;
            private set
            {
                if (_thumbPath == value) return;
                _thumbPath = value;
                OnPropertyChanged(nameof(ThumbPath));
            }
        }

        /// <summary>Recompute thumbnail path and notify UI.</summary>
        public void TouchThumb() => ThumbPath = ComputeThumbPath();

        private string? ComputeThumbPath()
        {
            if (string.IsNullOrWhiteSpace(FullPath) || !Directory.Exists(FullPath))
                return null;

            var baseName = Name;

            // Preferred candidates
            var candidates = new[]
            {
                Path.Combine(FullPath, $"{baseName}_{ThumbSize}.jpg"),
                Path.Combine(FullPath, $"{baseName}_{ThumbSize}.png"),
                Path.Combine(FullPath, $"{baseName}.jpg"),
                Path.Combine(FullPath, $"{baseName}.png"),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            // Wildcards
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" };

            try
            {
                foreach (var f in Directory.EnumerateFiles(FullPath, $"*_{ThumbSize}.*"))
                    if (exts.Contains(Path.GetExtension(f))) return f;

                foreach (var f in Directory.EnumerateFiles(FullPath))
                    if (exts.Contains(Path.GetExtension(f))) return f;
            }
            catch { /* ignore IO issues and return null */ }

            return null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
