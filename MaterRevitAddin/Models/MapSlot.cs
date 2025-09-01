using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mater2026.Models
{
    public class MapSlot(MapType type) : INotifyPropertyChanged
    {

        public MapType Type
        {
            get => type;
            set { if (type == value) return; type = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        private MapFile? _assigned;
        public MapFile? Assigned
        {
            get => _assigned;
            set { if (_assigned == value) return; _assigned = value; OnPropertyChanged(); }
        }

        private bool _invert;
        public bool Invert
        {
            get => _invert;
            set { if (_invert == value) return; _invert = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        private string? _detail; // "Normal" / "Depth" / null
        public string? Detail
        {
            get => _detail;
            set { if (_detail == value) return; _detail = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public (int r, int g, int b)? Tint
        {
            get => _tint;
            set { _tint = value; OnPropertyChanged(); }
        }

        private (int r, int g, int b)? _tint;

        public string DisplayName
        {
            get
            {
                if (Type == MapType.Bump)
                {
                    if (string.Equals(Detail, "Normal", StringComparison.OrdinalIgnoreCase)) return "Normal";
                    if (string.Equals(Detail, "Depth", StringComparison.OrdinalIgnoreCase)) return "Depth";
                    return "Bump";
                }
                if (Type == MapType.Roughness && Invert) return "Glossiness";
                return Type.ToString(); // Albedo / Reflection / Refraction / Illumination
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
