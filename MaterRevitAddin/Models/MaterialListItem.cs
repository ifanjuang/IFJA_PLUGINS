using Autodesk.Revit.DB;

namespace Mater2026.Models
{
    public class MaterialListItem
    {
        public ElementId Id { get; init; } = ElementId.InvalidElementId;
        public string Name { get; init; } = string.Empty;
    }
}