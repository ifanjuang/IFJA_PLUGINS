using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mater2026.ViewModels
{
    public class UiParameters : INotifyPropertyChanged
    {
        private string _materialName = "";
        public string MaterialName
        {
            get => _materialName;
            set { _materialName = value; OnPropertyChanged(); OnMaterialNameChanged?.Invoke(value); }
        }

        private string _folderPath = "";
        public string FolderPath { get => _folderPath; set { _folderPath = value; OnPropertyChanged(); } }

        private double _widthCm;
        public double WidthCm { get => _widthCm; set { _widthCm = value; OnPropertyChanged(); } }

        private double _heightCm;
        public double HeightCm { get => _heightCm; set { _heightCm = value; OnPropertyChanged(); } }

        private double _rotationDeg;
        public double RotationDeg { get => _rotationDeg; set { _rotationDeg = value; OnPropertyChanged(); } }

        private int _tilesX = 1;
        public int TilesX { get => _tilesX; set { _tilesX = value; OnPropertyChanged(); } }

        private int _tilesY = 1;
        public int TilesY { get => _tilesY; set { _tilesY = value; OnPropertyChanged(); } }

        /// <summary>
        /// Teinte overlay (R,G,B). Utilisée sur l’albedo (UnifiedBitmap Tint).
        /// </summary>
        public (int r, int g, int b)? Tint { get; set; }

        /// <summary>
        /// Notifié à chaque modification du nom pour auto-sélection d’un matériau existant.
        /// </summary>
        public event Action<string>? OnMaterialNameChanged;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
