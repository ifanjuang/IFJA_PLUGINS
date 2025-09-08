using Autodesk.Revit.DB;

namespace Mater2026.Models
{
    public sealed class MaterialListItem
    {
        public ElementId Id { get; set; } = ElementId.InvalidElementId;
        public string Name { get; set; } = string.Empty;
        public override string ToString() => Name;
    }
}
