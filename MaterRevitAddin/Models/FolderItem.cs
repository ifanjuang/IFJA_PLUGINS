using System.ComponentModel;
using System.IO;

namespace Mater2026.Models
{
    public class FolderItem : INotifyPropertyChanged
    {
        public string FullPath { get; set; } = "";
        public string Name => Path.GetFileName(FullPath);

        private int _thumbSize = 512;
        public int ThumbSize
        {
            get => _thumbSize;
            set { _thumbSize = value; OnPropertyChanged(nameof(ThumbPath)); }
        }

        public string ThumbPath => Path.Combine(FullPath, $"{Name}_{ThumbSize}.jpg");

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>
        /// Force le rafraîchissement de la vignette (après génération).
        /// </summary>
        public void TouchThumb() => OnPropertyChanged(nameof(ThumbPath));
    }
}
