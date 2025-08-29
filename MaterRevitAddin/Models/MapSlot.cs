namespace Mater2026.Models
{
    public sealed class MapSlot(MapType t)
    {
        public MapType Type { get; } = t;
        public MapFile? Assigned { get; set; }
        public bool Invert { get; set; }
        public (int r, int g, int b)? Tint { get; set; } // overlay color
    }
}
