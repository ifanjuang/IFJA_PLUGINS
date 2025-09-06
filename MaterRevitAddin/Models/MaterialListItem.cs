using Autodesk.Revit.DB;

namespace Mater2026.Models
{
    public sealed class MaterialListItem
    {
        public long Id { get; }
        public string Name { get; }

        public MaterialListItem(Material m)
        {
            Id = m.Id.Value;                // Revit 2026 → .Value (Int64)
            Name = m.Name ?? $"Material_{Id}";
        }
        public override string ToString() => Name;
    }
}
