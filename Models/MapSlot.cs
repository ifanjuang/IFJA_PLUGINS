using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MaterRevitAddin.Models
{
    public class MapSlot : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = "";
        public MapType SlotType { get; set; }
        public string IconPath { get; set; } = "";
        public ObservableCollection<string> Alternatives { get; } = new();
        public string? SelectedAlternative { get; set; }
        public string? AssignedPath => SelectedAlternative;
        public bool InheritFromDiffuse { get; set; } = true;
        public double ScaleX_m { get; set; }
        public double ScaleY_m { get; set; }
        public double Rotation_deg { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
