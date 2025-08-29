using System.Collections.Generic;
using Mater2026.Models;

namespace Mater2026.Services
{
    public class UiReadback
    {
        public string? FolderPath { get; set; }
        public double WidthCm { get; set; }
        public double HeightCm { get; set; }
        public double RotationDeg { get; set; }
        public (int r, int g, int b)? Tint { get; set; }
        public Dictionary<MapType, (string? path, bool invert)> Maps { get; } = new();
        public int TilesX { get; set; } = 1;
        public int TilesY { get; set; } = 1;
    }
}
