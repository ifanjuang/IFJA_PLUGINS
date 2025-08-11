using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using MaterRevitAddin.Models;
using MaterRevitAddin.Utils;

namespace MaterRevitAddin.ViewModels
{
    public class MaterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly UIApplication _uiapp;
        public MaterViewModel(UIApplication uiapp)
        {
            _uiapp = uiapp;
            RootPath = @"A:\assets\textures\";
            LoadFolderTree();

            PickMaterialFromModelCommand = new RelayCommand(PickFromModel);
            CreatePatternFromDivisionsCommand = new RelayCommand(CreatePattern);
            DoPrimaryActionCommand = new RelayCommand(DoPrimaryAction);
            CopyMaterialCommand = new RelayCommand(CopyMaterial);
            StartPaintSessionCommand = new RelayCommand(StartPaintSession);

            ReplaceWithPrepared_NewCommand = new RelayCommand(BatchReplaceNew);
            ReplaceWithPrepared_OverwriteCommand = new RelayCommand(BatchOverwrite);
            RenameAppearanceCommand = new RelayCommand(RenameAppearance);
            RenameLinkedAppearanceCommand = new RelayCommand(RenameLinkedAppearance);
            SelectAllUsingSelectedMaterialsCommand = new RelayCommand(SelectAllUsingMaterials);
            SelectAllUsingSelectedAppearancesCommand = new RelayCommand(SelectAllUsingAppearances);

            RefreshCollectors();
        }

        public ObservableCollection<DirectoryItem> FolderRootNodes { get; } = new();
        public string RootPath { get; set; }
        public string CurrentFolderPath { get; set; } = "";

        void LoadFolderTree()
        {
            FolderRootNodes.Clear();
            if (!Directory.Exists(RootPath)) return;
            var di = new DirectoryInfo(RootPath);
            var node = new DirectoryItem { Name = di.Name, FullPath = di.FullName };
            FolderRootNodes.Add(node);
            CurrentFolderPath = di.FullName;
            LoadGallery(di.FullName);
        }

        public ObservableCollection<MapItem> MapGallery { get; } = new();
        public ObservableCollection<MapSlot> MapSlots { get; } = new();

        void LoadGallery(string folder)
        {
            MapGallery.Clear();
            foreach (var p in MapFileUtils.EnumImages(folder))
            {
                var d = MapFileUtils.Detect(p);
                MapGallery.Add(new MapItem { Name = Path.GetFileName(p), FullPath = p, DetectedLabel = d.label, DetectedIconPath = d.icon });
            }
            CurrentFolderPath = folder;
            if (MapGallery.Count >= 3) DetectAndFillSlots();
        }

        void DetectAndFillSlots()
        {
            MapSlots.Clear();
            var slotDiff = new MapSlot { DisplayName = "Diffuse", SlotType = MapType.DIFF, IconPath = "Resources/Icons/diffuse.png" };
            var slotGlos = new MapSlot { DisplayName = "Gloss/Rough", SlotType = MapType.GLOS, IconPath = "Resources/Icons/gloss.png" };
            var slotRefl = new MapSlot { DisplayName = "Reflect 90°", SlotType = MapType.REFL, IconPath = "Resources/Icons/reflect.png" };
            var slotBump = new MapSlot { DisplayName = "Bump / Normal / Depth", SlotType = MapType.BUMP, IconPath = "Resources/Icons/bump.png" };
            var slotOpac = new MapSlot { DisplayName = "Opacity / Transparency", SlotType = MapType.OPAC, IconPath = "Resources/Icons/opacity.png" };

            var all = MapGallery.Select(x => x.FullPath).ToList();
            foreach (var s in new[] { slotDiff, slotGlos, slotRefl, slotBump, slotOpac })
                foreach (var p in all) s.Alternatives.Add(p);

            slotDiff.SelectedAlternative = all.FirstOrDefault(p => MapFileUtils.Detect(p).slot == MapType.DIFF);
            slotGlos.SelectedAlternative = all.FirstOrDefault(p => MapFileUtils.Detect(p).label.StartsWith("Gloss"));
            if (slotGlos.SelectedAlternative == null)
                slotGlos.SelectedAlternative = all.FirstOrDefault(p => MapFileUtils.Detect(p).label.StartsWith("Rough"));
            slotRefl.SelectedAlternative = all.FirstOrDefault(p => MapFileUtils.Detect(p).slot == MapType.REFL);
            var b = all.FirstOrDefault(p => MapFileUtils.Detect(p).label.StartsWith("Normal")) ??
                    all.FirstOrDefault(p => MapFileUtils.Detect(p).label.StartsWith("Depth")) ??
                    all.FirstOrDefault(p => MapFileUtils.Detect(p).label.StartsWith("Bump"));
            slotBump.SelectedAlternative = b;
            slotOpac.SelectedAlternative = all.FirstOrDefault(p => MapFileUtils.Detect(p).slot == MapType.OPAC);

            MapSlots.Add(slotDiff);
            MapSlots.Add(slotGlos);
            MapSlots.Add(slotRefl);
            MapSlots.Add(slotBump);
            MapSlots.Add(slotOpac);
        }

        public string? GetSlotPath(MapType type) => MapSlots.FirstOrDefault(x => x.SlotType == type)?.AssignedPath;

        public string MaterialName { get; set; } = "MAT_PBR";
        public string AppearanceName { get; set; } = "APP_PBR";
        public string? Description { get; set; }
        public double RealWorldSizeX { get; set; } = 1.0;
        public double RealWorldSizeY { get; set; } = 1.0;
        public double RotationAngle { get; set; } = 0.0;
        public bool GlobalLockInheritDiffuse { get; set; } = true;

        public Color SelectedRevitColor { get; set; } = new Color(200,200,200);
        public bool UseTintBackground { get; set; } = false;

        public int DivX { get; set; } = 0;
        public int DivY { get; set; } = 0;

        public ObservableCollection<MaterialItem> ProjectMaterials { get; } = new();
        public ObservableCollection<AppearanceItem> ProjectAppearances { get; } = new();

        public void RefreshCollectors()
        {
            var doc = _uiapp.ActiveUIDocument.Document;
            ProjectMaterials.Clear();
            ProjectAppearances.Clear();
            foreach (var m in new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>())
                ProjectMaterials.Add(new MaterialItem { Id = m.Id, Name = m.Name });
            foreach (var a in new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement)).Cast<AppearanceAssetElement>())
                ProjectAppearances.Add(new AppearanceItem { Id = a.Id, Name = a.Name, Description = a.Description });
        }

        public ICommand PickMaterialFromModelCommand { get; }
        public ICommand CreatePatternFromDivisionsCommand { get; }
        public ICommand DoPrimaryActionCommand { get; }
        public ICommand CopyMaterialCommand { get; }
        public ICommand StartPaintSessionCommand { get; }

        public ICommand ReplaceWithPrepared_NewCommand { get; }
        public ICommand ReplaceWithPrepared_OverwriteCommand { get; }
        public ICommand RenameAppearanceCommand { get; }
        public ICommand RenameLinkedAppearanceCommand { get; }
        public ICommand SelectAllUsingSelectedMaterialsCommand { get; }
        public ICommand SelectAllUsingSelectedAppearancesCommand { get; }

        void PickFromModel()
        {
            ExternalEvents.PickMaterialHandler.Instance.VM = this;
            ExternalEvents.PickMaterialHandler.Event.Raise();
        }

        void CreatePattern()
        {
            ExternalEvents.CreatePatternFromDivisionsHandler.Instance.VM = this;
            ExternalEvents.CreatePatternFromDivisionsHandler.Event.Raise();
        }

        void DoPrimaryAction()
        {
            ExternalEvents.ReplaceOrPaintHandler.Instance.VM = this;
            ExternalEvents.ReplaceOrPaintHandler.Event.Raise();
        }

        void CopyMaterial()
        {
            ExternalEvents.CreateOrUpdateMaterialHandler.Instance.VM = this;
            ExternalEvents.CreateOrUpdateMaterialHandler.Event.Raise();
        }

        void StartPaintSession()
        {
            ExternalEvents.StartPaintSessionHandler.Instance.VM = this;
            ExternalEvents.StartPaintSessionHandler.Event.Raise();
        }

        void BatchReplaceNew()
        {
            ExternalEvents.BatchReplaceNewHandler.Instance.VM = this;
            ExternalEvents.BatchReplaceNewHandler.Event.Raise();
        }

        void BatchOverwrite()
        {
            ExternalEvents.BatchOverwriteHandler.Instance.VM = this;
            ExternalEvents.BatchOverwriteHandler.Event.Raise();
        }

        void RenameAppearance()
        {
            var doc = _uiapp.ActiveUIDocument.Document;
            var app = ProjectAppearances.FirstOrDefault(a => a.IsSelected);
            if (app == null) { Autodesk.Revit.UI.TaskDialog.Show("Renommer", "Sélectionnez une apparence."); return; }
            var win = System.Windows.Application.Current?.Windows.OfType<Views.MainWindow>().FirstOrDefault();
            var newName = Views.InputDialog.Show(win!, "Nouveau nom d'apparence :", app.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;

            using var t = new Transaction(doc, "Renommer apparence");
            t.Start();
            var elem = doc.GetElement(app.Id) as AppearanceAssetElement;
            if (elem != null) elem.Name = NameUtils.GetUniqueAppearanceName(doc, newName);
            t.Commit();
            RefreshCollectors();
        }

        void RenameLinkedAppearance()
        {
            var doc = _uiapp.ActiveUIDocument.Document;
            var mat = ProjectMaterials.FirstOrDefault(m => m.IsSelected);
            if (mat == null) { Autodesk.Revit.UI.TaskDialog.Show("Renommer", "Sélectionnez un matériau."); return; }
            var me = doc.GetElement(mat.Id) as Material;
            if (me == null) return;
            var appe = doc.GetElement(me.AppearanceAssetId) as AppearanceAssetElement;
            if (appe == null) { Autodesk.Revit.UI.TaskDialog.Show("Renommer", "Aucune apparence liée."); return; }

            var win = System.Windows.Application.Current?.Windows.OfType<Views.MainWindow>().FirstOrDefault();
            var newName = Views.InputDialog.Show(win!, "Nouveau nom d'apparence :", appe.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;

            using var t = new Transaction(doc, "Renommer apparence liée");
            t.Start();
            appe.Name = NameUtils.GetUniqueAppearanceName(doc, newName);
            t.Commit();
            RefreshCollectors();
        }

        void SelectAllUsingMaterials()
        {
            var uiapp = _uiapp;
            var set = ProjectMaterials.Where(x => x.IsSelected).Select(x => x.Id).ToList();
            Utils.HighlightService.HighlightByMaterials(uiapp, set);
        }

        void SelectAllUsingAppearances()
        {
            var uiapp = _uiapp;
            var doc = uiapp.ActiveUIDocument.Document;
            var selectedApps = ProjectAppearances.Where(x => x.IsSelected).Select(x => x.Id).ToHashSet();
            var mats = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .Where(m => selectedApps.Contains(m.AppearanceAssetId)).Select(m => m.Id).ToList();
            Utils.HighlightService.HighlightByMaterials(uiapp, mats);
        }

        public ElementId ResolveTargetMaterialId(Document doc)
        {
            var m = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .FirstOrDefault(x => x.Name.Equals(MaterialName, StringComparison.OrdinalIgnoreCase));
            return m?.Id ?? ElementId.InvalidElementId;
        }
    }
}
