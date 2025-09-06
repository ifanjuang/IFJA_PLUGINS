using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Mater2026.Models;
using Mater2026.Services;
using Mater2026.Handlers;
using WpfApp = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace Mater2026.ViewModels
{
    public class MaterViewModel : INotifyPropertyChanged
    {
        // === Collections bound to UI ===
        public ObservableCollection<FolderItem> GridFolders { get; } = [];
        public ObservableCollection<MaterialListItem> ProjectMaterials { get; } = [];
        public ObservableCollection<MapSlot> MapTypes { get; } = [];

        // visibilité du bouton Remplacer
        public bool HasSelectedMaterial => SelectedMaterial != null;

        // commande de toggle depuis la liste
        public RelayCommand<MaterialListItem> ToggleSelectMaterialCmd { get; }

        // handler + external event pour sélectionner tous les objets par apparence
        private readonly SelectByAppearanceHandler _selectByAppearanceHandler;
        private readonly ExternalEvent _selectByAppearanceEvent;

        private void ToggleSelectMaterial(MaterialListItem? item)
        {
            // 2e clic sur le même item => désélection + clear Revit
            if (item != null && SelectedMaterial?.Id == item.Id)
            {
                SelectedMaterial = null;
                OnPropertyChanged(nameof(HasSelectedMaterial));

                _selectByAppearanceHandler.ClearSelection = true;
                _selectByAppearanceEvent.Raise();
                _selectByAppearanceHandler.ClearSelection = false;

                AssignCommand.RaiseCanExecuteChanged();
                CreateCommand.RaiseCanExecuteChanged();
                ReplaceCommand.RaiseCanExecuteChanged();
                return;
            }

            // Nouveau choix
            SelectedMaterial = item;
            OnPropertyChanged(nameof(HasSelectedMaterial));

            // Pas de fetch de paramètres pour la colonne droite
            _selectByAppearanceEvent.Raise();

            AssignCommand.RaiseCanExecuteChanged();
            CreateCommand.RaiseCanExecuteChanged();
            ReplaceCommand.RaiseCanExecuteChanged();
        }



        // Parameters bucket
        public UiParameters Params { get; } = new();

        private readonly UIApplication _uiapp;
        private readonly UIDocument _uidoc;

        // Thumb job lifecycle
        private CancellationTokenSource? _thumbCts;
        private Task? _thumbTask;

        // === Commands the window wires ===
        public RelayCommand<string> SelectTreeNodeCommand { get; }
        public RelayCommand<FolderItem> GridTileLeftClickCommand { get; }
        public RelayCommand ToggleGalleryCommand { get; }
        public RelayCommand SetThumb512Command { get; }
        public RelayCommand SetThumb1024Command { get; }
        public RelayCommand SetThumb2048Command { get; }
        public RelayCommand GenerateMissingThumbsCommand { get; }
        public RelayCommand RegenerateAllThumbsCommand { get; }
        public RelayCommand CreateCommand { get; }
        public RelayCommand ReplaceCommand { get; }
        public RelayCommand AssignCommand { get; }
        public RelayCommand PipetteCommand { get; }

        // === UI state ===
        private bool _isGallery;
        public bool IsGallery { get => _isGallery; set { _isGallery = value; OnPropertyChanged(); } }

        // default to smallest per your spec
        private int _thumbSize = 128;
        public int ThumbSize
        {
            get => _thumbSize;
            set
            {
                if (_thumbSize == value) return;
                _thumbSize = value;
                foreach (var f in GridFolders) f.ThumbSize = value;
                OnPropertyChanged();
            }
        }

        private MaterialListItem? _selectedMaterial;
        public MaterialListItem? SelectedMaterial
        {
            get => _selectedMaterial;
            set { _selectedMaterial = value; OnPropertyChanged(); OnSelectedMaterialChanged(); }
        }

        // progress
        private bool _isThumbWorking;
        public bool IsThumbWorking { get => _isThumbWorking; set { _isThumbWorking = value; OnPropertyChanged(); } }
        private int _thumbDone;
        public int ThumbDone { get => _thumbDone; set { _thumbDone = value; OnPropertyChanged(); } }
        private int _thumbTotal;
        public int ThumbTotal { get => _thumbTotal; set { _thumbTotal = value; OnPropertyChanged(); } }

        // grid root path (needed by the window)
        public string? CurrentGridRoot { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // Restart (cancel + relaunch) the thumbnail pass safely
        private Mater2026.ViewModels.MaterViewModel VM => (Mater2026.ViewModels.MaterViewModel)DataContext;
        private void ReplaceBtn_Click(object s, RoutedEventArgs e) => VM.ReplaceCommand.Execute(null);

        public void RestartThumbJob(int size, bool overwrite)
        {
            var previousTask = _thumbTask;          // hold onto the old task
            var oldCts = _thumbCts;                 // and its CTS
            _thumbCts = new CancellationTokenSource();

            // cancel the previous run
            oldCts?.Cancel();

            // kick the new run
            _thumbTask = EnsureThumbsAsync(size, overwrite, _thumbCts.Token);

            // when the previous run finally ends, dispose its CTS
            if (previousTask != null && oldCts != null)
            {
                _ = Task.Run(async () =>
                {
                    try { await previousTask; }
                    catch { /* ignored */ }
                    finally { try { oldCts.Dispose(); } catch { } }
                });
            }
        }

        // Optional: handy await-er for callers who want to wait until idle
        public Task WhenThumbsIdleAsync() => _thumbTask ?? Task.CompletedTask;

        public MaterViewModel(UIApplication uiapp)
        {
            _uiapp = uiapp;
            _uidoc = uiapp.ActiveUIDocument;

            RefreshMaterials();

            SelectTreeNodeCommand = new RelayCommand<string>(SelectTreeNode);
            GridTileLeftClickCommand = new RelayCommand<FolderItem>(OnGridTileLeftClick);
            ToggleGalleryCommand = new RelayCommand(() => IsGallery = !IsGallery);

            SetThumb512Command = new RelayCommand(() => { ThumbSize = 512; RestartThumbJob(ThumbSize, overwrite: false); });
            SetThumb1024Command = new RelayCommand(() => { ThumbSize = 1024; RestartThumbJob(ThumbSize, overwrite: false); });
            SetThumb2048Command = new RelayCommand(() => { ThumbSize = 2048; RestartThumbJob(ThumbSize, overwrite: false); });

            GenerateMissingThumbsCommand = new RelayCommand(() => _ = GenerateMissingThumbsForDirectChildrenAsync(CurrentGridRoot ?? "", [128, 512, 1024]));
            RegenerateAllThumbsCommand = new RelayCommand(() => RestartThumbJob(ThumbSize, overwrite: true));

            CreateCommand = new RelayCommand(() => _ = CreateMaterialAndAppearanceFromParams(), CanCreate);
            ReplaceCommand = new RelayCommand(() => _ = ReplaceSelectedMaterialAppearanceFromParams(), CanReplace);
            AssignCommand = new RelayCommand(StartAssignMode, () => SelectedMaterial != null || ProjectMaterials.Count > 0);
            PipetteCommand = new RelayCommand(StartPipetteOnce);

            _selectByAppearanceHandler = new SelectByAppearanceHandler { VM = this };
            _selectByAppearanceEvent = ExternalEvent.Create(_selectByAppearanceHandler);

            ToggleSelectMaterialCmd = new RelayCommand<MaterialListItem>(ToggleSelectMaterial);


            Params.OnMaterialNameChanged += OnMaterialNameEdited;
        }

        void RefreshMaterials()
        {
            ProjectMaterials.Clear();
            foreach (var (id, name) in RevitMaterialService.GetProjectMaterials(_uidoc))
                ProjectMaterials.Add(new MaterialListItem { Id = id, Name = name });
        }

        // === Tree → grid of direct subfolders (show ALL direct children; leaf handled on click) ===
        public void SelectTreeNode(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            CurrentGridRoot = folderPath;
            Params.FolderPath = folderPath;
            if (!Directory.Exists(folderPath)) return;

            GridFolders.Clear();

            // Show ALL direct children in the grid (leaf & non-leaf)
            foreach (var sub in Directory.EnumerateDirectories(folderPath))
                GridFolders.Add(new FolderItem { FullPath = sub, ThumbSize = ThumbSize }); // Name is derived

            // launch (or relaunch) the thumb pass
            RestartThumbJob(ThumbSize, overwrite: false);
        }

        // === Compatibility method for the window (explicit async detection call) ===
        public async Task DetectMapsForFolderAsync(string folderPath)
        {
            await Task.Yield();

            MapTypes.Clear();
            foreach (var s in DetectionService.DetectSlots(folderPath))
                MapTypes.Add(s);

            // Standard params after detection
            Params.FolderPath = folderPath;
            Params.MaterialName = Path.GetFileName(folderPath);
            Params.WidthCm = 300;
            Params.HeightCm = 300;
            Params.RotationDeg = 0;
            Params.TilesX = 1;
            Params.TilesY = 1;
            Params.Tint = (255, 255, 255);
        }

        // === Grid tile click (leaf-only detection) ===
        public void OnGridTileLeftClick(FolderItem? item)
        {
            if (item == null) return;

            // leaf only → if it has subdirectories, user should navigate deeper
            if (Directory.EnumerateDirectories(item.FullPath).Any())
                return;

            if (!FileService.HasKeyImage(item.FullPath))
            {
                WpfMessageBox.Show(
                    "Image-clé manquante : la détection s'exécute uniquement si le dossier contient une image nommée comme le dossier.",
                    "Détection PBR", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MapTypes.Clear();
            foreach (var s in DetectionService.DetectSlots(item.FullPath)) MapTypes.Add(s);

            // Standard params
            Params.FolderPath = item.FullPath;
            Params.MaterialName = Path.GetFileName(item.FullPath);
            Params.WidthCm = 300;
            Params.HeightCm = 300;
            Params.RotationDeg = 0;
            Params.TilesX = 1;
            Params.TilesY = 1;
            Params.Tint = (255, 255, 255);
        }

        // === Generate missing thumbs on direct children ===
        public Task GenerateMissingThumbsForDirectChildrenAsync(string root, int size)
            => GenerateMissingThumbsForDirectChildrenAsync(root, [size]);

        public async Task GenerateMissingThumbsForDirectChildrenAsync(string root, int[] sizes)
        {
            // avoid fighting the active decode
            await WhenThumbsIdleAsync();

            await Task.Run(() =>
            {
                foreach (var sub in Directory.EnumerateDirectories(root))
                    foreach (var s in sizes)
                    {
                        var name = Path.GetFileName(sub);
                        var target = Path.Combine(sub, $"{name}_{s}.jpg");
                        if (!File.Exists(target))
                            ThumbnailService.GenerateThumb(sub, s, overwrite: false);
                    }
            });

            // refresh grid and re-run thumb job for current size (no overwrite)
            SelectTreeNode(root);
            RestartThumbJob(ThumbSize, overwrite: false);
        }

        // === Thumbs worker (snapshot + UI-dispatch) ===
        async Task EnsureThumbsAsync(int size, bool overwrite, CancellationToken token = default)
        {
            IsThumbWorking = true;

            // snapshot to avoid “Collection was modified” while you repopulate GridFolders
            var folders = GridFolders.ToArray();
            ThumbDone = 0;
            ThumbTotal = folders.Length;

            foreach (var f in folders)
            {
                if (token.IsCancellationRequested) break;

                // heavy IO/encode off the UI thread; some codecs need STA
                await Task.Run(() =>
                {
                    var th = new System.Threading.Thread(() =>
                    {
                        try { ThumbnailService.GenerateThumb(f.FullPath, size, overwrite); } catch { }
                    })
                    {
                        IsBackground = true
                    };
                    th.SetApartmentState(System.Threading.ApartmentState.STA);
                    th.Start();
                    th.Join();
                }, token);

                if (token.IsCancellationRequested) break;

                await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                {
                    f.ThumbSize = size;
                    f.TouchThumb(); // re-pick _{size}.jpg or fallback
                    ThumbDone++;
                }, DispatcherPriority.Background, token);
            }
            IsThumbWorking = false;
        }

        // === Create / Replace ===
        bool Ready()
        {
            var hasAlbedo = MapTypes.Any(s => s.Type == MapType.Albedo && s.Assigned != null);
            return hasAlbedo && Params.WidthCm > 0 && Params.HeightCm > 0 && Params.TilesX >= 1 && Params.TilesY >= 1;
        }
        bool CanCreate() => SelectedMaterial == null && Ready();
        bool CanReplace() => SelectedMaterial != null && Ready();

        // expose compatibility APIs the window calls
        public async Task CreateMaterialAndAppearanceFromParams() { await Task.Yield(); CreateInternal(); }
        public async Task ReplaceSelectedMaterialAppearanceFromParams() { await Task.Yield(); ReplaceInternal(); }

        void CreateInternal()
        {
            var folder = Params.FolderPath;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                WpfMessageBox.Show("Dossier invalide.", "Créer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var doc = _uidoc.Document;
            var appearanceName = string.IsNullOrWhiteSpace(Params.MaterialName) ? Path.GetFileName(folder) : Params.MaterialName;

            var app = RevitMaterialService.GetOrCreateAppearanceByFolder(doc, appearanceName);
            RevitMaterialService.WriteAppearanceFolderPath(app, folder);

            IDictionary<MapType, (string? path, bool invert, string? detail)> maps =
            MapTypes
                .Where(s => s.Assigned != null)
                .ToDictionary(
                    s => s.Type,
                    s => (path: (string?)s.Assigned!.FullPath, invert: s.Invert, detail: s.Detail)
                );

            RevitMaterialService.ApplyUiToAppearance(
                app, maps, Params.WidthCm, Params.HeightCm, Params.RotationDeg, Params.Tint);



            var mat = RevitMaterialService.CreateMaterial(doc, appearanceName, app);

            RefreshMaterials();
            SelectedMaterial = ProjectMaterials.FirstOrDefault(m => m.Id == mat.Id);

            WpfMessageBox.Show("Matériau créé et sélectionné.", "Créer", MessageBoxButton.OK, MessageBoxImage.Information);
            CreateCommand.RaiseCanExecuteChanged();
            ReplaceCommand.RaiseCanExecuteChanged();
        }

        void ReplaceInternal()
        {
            if (SelectedMaterial == null) return;

            var folder = Params.FolderPath ?? "";
            var doc = _uidoc.Document;

            var (mat, _) = RevitMaterialService.GetMaterialAndAppearance(doc, SelectedMaterial.Id);
            if (mat == null) return;

            var appearanceName = string.IsNullOrWhiteSpace(Params.MaterialName) ? Path.GetFileName(folder) : Params.MaterialName;

            var app = RevitMaterialService.GetOrCreateAppearanceByFolder(doc, appearanceName);
            RevitMaterialService.WriteAppearanceFolderPath(app, folder);

            IDictionary<MapType, (string? path, bool invert, string? detail)> maps =
            MapTypes
                .Where(s => s.Assigned != null)
                .ToDictionary(
                    s => s.Type,
                    s => (path: (string?)s.Assigned!.FullPath, invert: s.Invert, detail: s.Detail)
                );

            RevitMaterialService.ApplyUiToAppearance(
                app, maps, Params.WidthCm, Params.HeightCm, Params.RotationDeg, Params.Tint);


            RevitMaterialService.ReplaceMaterialAppearance(mat, app);

            WpfMessageBox.Show("Matériau mis à jour.", "Remplacer", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === Auto-select on name edit ===
        void OnMaterialNameEdited(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var match = ProjectMaterials.FirstOrDefault(m => m.Name.Equals(text.Trim(), StringComparison.CurrentCultureIgnoreCase));
            if (match != null) SelectedMaterial = match;
        }

        // === When user selects a material from the list ===
        void OnSelectedMaterialChanged()
        {
            AssignCommand.RaiseCanExecuteChanged();
            CreateCommand.RaiseCanExecuteChanged();
            ReplaceCommand.RaiseCanExecuteChanged();

        }

        // === Assign (paint) mode ===
        void StartAssignMode()
        {
            var first = ProjectMaterials.FirstOrDefault();
            var startMat = SelectedMaterial?.Id ?? first?.Id ?? ElementId.InvalidElementId;

            if (startMat == ElementId.InvalidElementId)
            {
                WpfMessageBox.Show("Aucun matériau disponible...", "ASSIGNER",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var handler = new PaintModeHandler
            {
                UiDoc = _uidoc,
                CurrentMaterialId = startMat,
                OnMaterialSampled = (matId) =>
                {
                    var match = ProjectMaterials.FirstOrDefault(m => m.Id == matId);
                    if (match != null) SelectedMaterial = match;
                }
            };

            var ev = ExternalEvent.Create(handler);
            handler.OnBegin = () => WpfApp.Current?.Dispatcher.Invoke(() => AppHide?.Invoke());
            handler.OnEnd = (commit) => WpfApp.Current?.Dispatcher.Invoke(() => AppShow?.Invoke());
            ev.Raise();
        }

        // === Single pipette click ===
        void StartPipetteOnce()
        {
            var handler = new PipetteHandler
            {
                UiDoc = _uidoc,
                OnPicked = (matId) =>
                {
                    var match = ProjectMaterials.FirstOrDefault(m => m.Id == matId);
                    if (match != null) SelectedMaterial = match;
                    else WpfMessageBox.Show("Le matériau prélevé n'existe pas dans ce projet.", "Pipette",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            var ev = ExternalEvent.Create(handler);
            handler.OnBegin = () => WpfApp.Current?.Dispatcher.Invoke(() => AppHide?.Invoke());
            handler.OnEnd = (ok) => WpfApp.Current?.Dispatcher.Invoke(() => AppShow?.Invoke());
            ev.Raise();
        }

        // Hooks so the window can hide/show while in Revit pick/paint modes
        public Action? AppHide { get; set; }
        public Action? AppShow { get; set; }
    }
}
