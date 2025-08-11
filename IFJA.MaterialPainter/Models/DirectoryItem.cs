using System.ComponentModel;

namespace MaterRevitAddin.Models
{
    public class DirectoryItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string? ThumbnailPath { get; set; }
        public bool IsParent { get; set; }
        public bool ExistsInProject { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
