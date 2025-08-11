using System.ComponentModel;
using Autodesk.Revit.DB;

namespace MaterRevitAddin.Models
{
    public class MaterialItem : INotifyPropertyChanged
    {
        public ElementId Id { get; set; } = ElementId.InvalidElementId;
        public string Name { get; set; } = "";
        public bool IsSelected { get; set; }
        public bool IsHighlighted { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class AppearanceItem : INotifyPropertyChanged
    {
        public ElementId Id { get; set; } = ElementId.InvalidElementId;
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsSelected { get; set; }
        public bool IsHighlighted { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
