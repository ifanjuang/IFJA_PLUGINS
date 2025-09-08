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
using System.Windows.Data;
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

        // Filtered view for the materials list
        private readonly ICollectionView? _materialsView;
        private string _materialFilter = "";
        public string MaterialFilter
        {
            get => _materialFilter;
            set { if (_materialFilter == value) return; _materialFilter = value; OnPropertyChanged(); _materialsView?.Refresh(); }
        }

        // Right panel parameters
        public UiParameters Params { get; } = new();

        // Revit context
        private readonly UIApplication _uiapp;
        private readonly UIDocument _uidoc;

        // Thumb job lifecycle
        private CancellationTokenSource? _thumbCts;
        private Task? _thumbTask;

        // Commands
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
        public RelayCommand<MaterialListItem> ToggleSelectMaterialCmd { get; }
        public RelayCommand<MaterialListItem> RetrieveParamsCmd { get; }

        // UI state
        private bool _isGallery;
        public bool IsGallery { get => _isGallery; set { _isGallery = value; OnPropertyChanged(); } }

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

        // Material selection
        private MaterialListItem? _selectedMaterial;
        public MaterialListItem? SelectedMaterial
        {
            get => _selectedMaterial;
            set { _selectedMaterial = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedMaterial)); OnSelectedMaterialChanged(); }
        }
        public bool HasSelectedMaterial => SelectedMaterial != null;

        // Progress
        private bool _isThumbWorking;
        public bool IsThumbWorking { get => _isThumbWorking; set { _isThumbWorking = value; OnPropertyChanged(); } }
        private int _thumbDone;
        public int ThumbDone { get => _thumbDone; set { _thumbDone = value; OnPropertyChanged(); } }
        private int _thumbTotal;
        public int ThumbTotal { get => _thumbTotal; set { _thumbTotal = value; OnPropertyChanged(); } }

        // Grid root path
        public string? CurrentGridRoot { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public MaterViewModel(UIApplication uiapp)
        {
            _uiapp = uiapp;
            _uidoc = uiapp.ActiveUIDocument;

            RefreshMaterials();

            // Create a view with filter support
            _materialsView = CollectionViewSource.GetDefaultView(ProjectMaterials);
            _materialsView.Filter = o =>
            {
                if (o is not MaterialListItem m) return false;
                if (string.IsNullOrWhiteSpace(MaterialFilter)) return true;
                return m.Name?.IndexOf(MaterialFilter, StringComparison.CurrentCultureIgnoreCase) >= 0;
            };

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

            ToggleSelectMaterialCmd = new RelayCommand<MaterialListItem>(ToggleSelectMaterial);
            RetrieveParamsCmd = new RelayCommand<MaterialListItem>(RetrieveParamsFromAppearance);

            _selectByAppearanceHandler = new SelectByAppearanceHandler { VM = this };
            _selectByAppearanceEvent = ExternalEvent.Create(_selectByAppearanceHandler);

            Params.OnMaterialNameChanged += OnMaterialNameEdited;
        }

        // ExternalEvents for selection highlight
        private readonly SelectByAppearanceHandler _selectByAppearanceHandler;
        private readonly ExternalEvent _selectByAppearanceEvent;

        // -------- Materials refresh --------
        void RefreshMaterials()
        {
            ProjectMaterials.Clear();
            foreach (var (id, name) in RevitMaterialService.GetProjectMaterials(_uidoc))
                ProjectMaterials.Add(new MaterialListItem { Id = id, Name = name });
        }

        // -------- Tree → grid display --------
        public void SelectTreeNode(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            CurrentGridRoot = folderPath;
            Params.FolderPath = folderPath;
            if (!Directory.Exists(folderPath)) return;

            GridFolders.Clear();
            foreach (var sub in Directory.EnumerateDirectories(folderPath))
                GridFolders.Add(new FolderItem { FullPath = sub, ThumbSize = ThumbSize });

            RestartThumbJob(ThumbSize, overwrite: false);
        }

        public async Task DetectMapsForFolderAsync(string folderPath)
        {
            await Task.Yield();

            MapTypes.Clear();
            foreach (var s in DetectionService.DetectSlots(folderPath))
                MapTypes.Add(s);

            ApplyDefaultsFromFolder(folderPath);
        }

        public void OnGridTileLeftClick(FolderItem? item)
        {
            if (item == null) return;

            if (Directory.EnumerateDirectories(item.FullPath).Any())
                return;

            if (!FileService.HasKeyImage(item.FullPath))
            {
                WpfMessageBox.Show(
                    "Key image missing: detection runs only if the folder contains an image named like the folder.",
                    "PBR Detection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MapTypes.Clear();
            foreach (var s in DetectionService.DetectSlots(item.FullPath)) MapTypes.Add(s);

            ApplyDefaultsFromFolder(item.FullPath);
        }

        // -------- Thumbnails worker --------
        public void RestartThumbJob(int size, bool overwrite)
        {
            var previousTask = _thumbTask;
            var oldCts = _thumbCts;
            _thumbCts = new CancellationTokenSource();

            oldCts?.Cancel();
            _thumbTask = EnsureThumbsAsync(size, overwrite, _thumbCts.Token);

            if (previousTask != null && oldCts != null)
            {
                _ = Task.Run(async () =>
                {
                    try { await previousTask; }
                    catch { }
                    finally { try { oldCts.Dispose(); } catch { } }
                });
            }
        }

        public Task WhenThumbsIdleAsync() => _thumbTask ?? Task.CompletedTask;

        async Task EnsureThumbsAsync(int size, bool overwrite, CancellationToken token = default)
        {
            IsThumbWorking = true;

            var folders = GridFolders.ToArray();
            ThumbDone = 0;
            ThumbTotal = folders.Length;

            foreach (var f in folders)
            {
                if (token.IsCancellationRequested) break;

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
                    f.TouchThumb();
                    ThumbDone++;
                }, DispatcherPriority.Background, token);
            }
            IsThumbWorking = false;
        }

        // === Generate missing thumbs on direct children ===
        public Task GenerateMissingThumbsForDirectChildrenAsync(string root, int size)
            => GenerateMissingThumbsForDirectChildrenAsync(root, [size]);

        public async Task GenerateMissingThumbsForDirectChildrenAsync(string root, int[] sizes)
        {
            if (string.IsNullOrWhiteSpace(root)) return;

            // avoid fighting the active decode
            await WhenThumbsIdleAsync();

            await Task.Run(() =>
            {
                foreach (var sub in System.IO.Directory.EnumerateDirectories(root))
                    foreach (var s in sizes)
                    {
                        var name = System.IO.Path.GetFileName(sub);
                        var target = System.IO.Path.Combine(sub, $"{name}_{s}.jpg");
                        if (!System.IO.File.Exists(target))
                            ThumbnailService.GenerateThumb(sub, s, overwrite: false);
                    }
            });

            // refresh grid and re-run thumb job for current size (no overwrite)
            SelectTreeNode(root);
            RestartThumbJob(ThumbSize, overwrite: false);
        }


        // -------- Create / Replace --------
        bool Ready()
        {
            var hasAlbedo = MapTypes.Any(s => s.Type == MapType.Albedo && s.Assigned != null);
            return hasAlbedo && Params.WidthCm > 0 && Params.HeightCm > 0 && Params.TilesX >= 1 && Params.TilesY >= 1;
        }
        bool CanCreate() => SelectedMaterial == null && Ready();
        bool CanReplace() => SelectedMaterial != null && Ready();

        public async Task CreateMaterialAndAppearanceFromParams() { await Task.Yield(); CreateInternal(); }
        public async Task ReplaceSelectedMaterialAppearanceFromParams() { await Task.Yield(); ReplaceInternal(); }

        private IDictionary<MapType, (string? path, bool invert, string? detail)> BuildUiMapsFromSlots => MapTypes.Where(s => s.Assigned != null)
                               .ToDictionary(s => s.Type, s => (path: (string?)s.Assigned!.FullPath, s.Invert, s.Detail));

        private void ApplyDefaultsFromFolder(string folder)
        {
            Params.FolderPath = folder;
            Params.MaterialName = Path.GetFileName(folder);
            Params.WidthCm = 300;
            Params.HeightCm = 300;
            Params.RotationDeg = 0;
            Params.TilesX = 1;
            Params.TilesY = 1;
            Params.Tint = (255, 255, 255);
        }

        void CreateInternal()
        {
            var folder = Params.FolderPath;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                WpfMessageBox.Show("Invalid folder.", "Create", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var doc = _uidoc.Document;
            var name = string.IsNullOrWhiteSpace(Params.MaterialName) ? Path.GetFileName(folder) : Params.MaterialName;

            var app = RevitMaterialService.GetOrCreateAppearanceByFolder(doc, name);
            RevitMaterialService.WriteAppearanceFolderPath(app, folder);

            var mat = RevitMaterialService.CreateMaterial(doc, name, app);

            var maps = BuildUiMapsFromSlots;
            RevitMaterialService.ApplyMapsToMaterial(doc, mat, maps, Params.WidthCm, Params.HeightCm, Params.RotationDeg, Params.Tint);

            RefreshMaterials();
            SelectedMaterial = ProjectMaterials.FirstOrDefault(m => m.Id == mat.Id);

            WpfMessageBox.Show("Material created and selected.", "Create", MessageBoxButton.OK, MessageBoxImage.Information);
            CreateCommand.RaiseCanExecuteChanged();
            ReplaceCommand.RaiseCanExecuteChanged();
        }

        void ReplaceInternal()
        {
            if (SelectedMaterial == null) return;

            var doc = _uidoc.Document;
            var (mat, _) = RevitMaterialService.GetMaterialAndAppearance(doc, SelectedMaterial.Id);
            if (mat == null) return;

            var maps = BuildUiMapsFromSlots;
            RevitMaterialService.ApplyMapsToMaterial(doc, mat, maps, Params.WidthCm, Params.HeightCm, Params.RotationDeg, Params.Tint);

            WpfMessageBox.Show("Material updated.", "Replace", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // -------- Toggle selection (no auto-fetch) --------
        private void ToggleSelectMaterial(MaterialListItem? item)
        {
            if (item != null && SelectedMaterial?.Id == item.Id)
            {
                SelectedMaterial = null;

                _selectByAppearanceHandler.ClearSelection = true;
                _selectByAppearanceEvent.Raise();
                _selectByAppearanceHandler.ClearSelection = false;

                AssignCommand.RaiseCanExecuteChanged();
                CreateCommand.RaiseCanExecuteChanged();
                ReplaceCommand.RaiseCanExecuteChanged();
                return;
            }

            SelectedMaterial = item;

            // Do NOT fetch right panel – only select/highlight in Revit
            _selectByAppearanceEvent.Raise();

            AssignCommand.RaiseCanExecuteChanged();
            CreateCommand.RaiseCanExecuteChanged();
            ReplaceCommand.RaiseCanExecuteChanged();
        }

        // -------- Context menu: retrieve parameters on demand --------
        private void RetrieveParamsFromAppearance(MaterialListItem? item)
        {
            if (item == null) return;

            SelectedMaterial = item;

            var rb = RevitMaterialService.ReadUiFromAppearanceAndMaterial(_uidoc.Document, item.Id);

            if (!string.IsNullOrWhiteSpace(rb.FolderPath)) Params.FolderPath = rb.FolderPath;
            if (rb.WidthCm > 0) Params.WidthCm = rb.WidthCm;
            if (rb.HeightCm > 0) Params.HeightCm = rb.HeightCm;
            Params.RotationDeg = rb.RotationDeg;
            Params.TilesX = rb.TilesX;
            Params.TilesY = rb.TilesY;
            Params.Tint = rb.Tint;

            MapTypes.Clear();
            foreach (var t in new[] { MapType.Albedo, MapType.Roughness, MapType.Reflection, MapType.Bump, MapType.Refraction, MapType.Illumination })
            {
                var slot = new MapSlot(t);
                if (rb.Maps.TryGetValue(t, out var mp) && mp.path != null)
                {
                    slot.Assigned = new MapFile(mp.path, t);
                    slot.Invert = mp.invert;
                }
                if (t == MapType.Albedo && rb.Tint.HasValue) slot.Tint = rb.Tint;
                else if (t == MapType.Bump && slot.Assigned != null)
                {
                    var n = Path.GetFileNameWithoutExtension(slot.Assigned.FullPath);
                    if (n.Contains("normal", StringComparison.OrdinalIgnoreCase)) slot.Detail = "Normal";
                    else if (n.Contains("height", StringComparison.OrdinalIgnoreCase) ||
                             n.Contains("depth", StringComparison.OrdinalIgnoreCase)) slot.Detail = "Height";
                    else slot.Detail = "Bump";
                }
                MapTypes.Add(slot);
            }
        }

        // -------- Name edit auto-select --------
        void OnMaterialNameEdited(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var match = ProjectMaterials.FirstOrDefault(m => m.Name.Equals(text.Trim(), StringComparison.CurrentCultureIgnoreCase));
            if (match != null) SelectedMaterial = match;
        }

        // -------- Selection changed (no fetch) --------
        void OnSelectedMaterialChanged()
        {
            AssignCommand.RaiseCanExecuteChanged();
            CreateCommand.RaiseCanExecuteChanged();
            ReplaceCommand.RaiseCanExecuteChanged();
            // intentionally no read of right panel here
        }

        // -------- Assign (paint) mode --------
        void StartAssignMode()
        {
            var first = ProjectMaterials.FirstOrDefault();
            var startMat = SelectedMaterial?.Id ?? first?.Id ?? ElementId.InvalidElementId;

            if (startMat == ElementId.InvalidElementId)
            {
                WpfMessageBox.Show("No material available.", "ASSIGN", MessageBoxButton.OK, MessageBoxImage.Information);
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

        // -------- Single eyedrop --------
        void StartPipetteOnce()
        {
            var handler = new PipetteHandler
            {
                UiDoc = _uidoc,
                OnPicked = (matId) =>
                {
                    var match = ProjectMaterials.FirstOrDefault(m => m.Id == matId);
                    if (match != null) SelectedMaterial = match;
                    else WpfMessageBox.Show("Picked material does not exist in this project.", "Eyedrop", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            var ev = ExternalEvent.Create(handler);
            handler.OnBegin = () => WpfApp.Current?.Dispatcher.Invoke(() => AppHide?.Invoke());
            handler.OnEnd = (ok) => WpfApp.Current?.Dispatcher.Invoke(() => AppShow?.Invoke());
            ev.Raise();
        }

        // Window hide/show hooks during pick/paint
        public Action? AppHide { get; set; }
        public Action? AppShow { get; set; }

        // Basic logging shims (avoid hard dependency on a logger)
        public static void LogInfo(string msg) => System.Diagnostics.Debug.WriteLine("[INFO] " + msg);
        public static void LogError(Exception ex) => System.Diagnostics.Debug.WriteLine("[ERR] " + ex);

    }
}
