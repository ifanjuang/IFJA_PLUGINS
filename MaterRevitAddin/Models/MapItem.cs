using System.ComponentModel;

namespace MaterRevitAddin.Models
{
    public enum MapType { NONE, DIFF, GLOS, REFL, BUMP, OPAC }

    public class MapItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string DetectedLabel { get; set; } = "Unknown";
        public string DetectedIconPath { get; set; } = "Resources/Icons/unknown.png";
        public bool IsAssigned { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}