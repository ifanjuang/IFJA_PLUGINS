using System.IO;

namespace Mater2026.Models
{
    public sealed class MapFile(string path, MapType type)
    {
        public string FullPath { get; init; } = path;
        public string FileName => Path.GetFileName(FullPath);
        public MapType Type { get; init; } = type;
        public override string ToString() => $"{Type}: {FileName}";
    }
}
