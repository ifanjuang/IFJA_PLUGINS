using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;

using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.ExtensibleStorage;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;

namespace Mater2026
{
    // ================================
    // INFRA
    // ================================
    public abstract class NotifyBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    public class RelayCommand(Action<object?> exec, Func<object?, bool>? canExec = null) : System.Windows.Input.ICommand
    {
        private readonly Action<object?> _exec = exec;
        private readonly Func<object?, bool>? _canExec = canExec;

        public bool CanExecute(object? p) => _canExec == null || _canExec(p);
        public void Execute(object? p) => _exec(p);
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public static class RevitStorageService
    {
        private static readonly Guid SchemaGuid = new("6A9A93B9-FE47-4D69-B548-FB9382C9F7BA");

        public static void SaveString(Document doc, string fieldName, string value)
        {
            var schema = Schema.Lookup(SchemaGuid) ?? CreateSchema();
            var entity = doc.ProjectInformation.GetEntity(schema);
            if (!entity.IsValid())
                entity = new Entity(schema);

            entity.Set(schema.GetField(fieldName), value);
            doc.ProjectInformation.SetEntity(entity);
        }

        public static string? LoadString(Document doc, string fieldName)
        {
            var schema = Schema.Lookup(SchemaGuid);
            if (schema == null) return null;

            var entity = doc.ProjectInformation.GetEntity(schema);
            if (!entity.IsValid()) return null;

            return entity.Get<string>(schema.GetField(fieldName));
        }

        private static Schema CreateSchema()
        {
            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName("Mater2026Settings");
            builder.AddSimpleField("RootFolder", typeof(string));
            return builder.Finish();
        }
    }

    // ================================
    // MODELS
    // ================================
    public enum MapType { Albedo, Bump, Roughness, Reflection, Refraction, Illumination }

    public class MapFile(string path, MapType t)
    {
        public string FullPath { get; } = path; public MapType Type { get; } = t;
    }

    public class MapSlot(MapType t) : NotifyBase
    {
        public MapType Type { get; } = t;
        public string DisplayName => Type.ToString();

        private MapFile? _assigned;
        public MapFile? Assigned { get => _assigned; set { if (SetField(ref _assigned, value)) OnPropertyChanged(nameof(ButtonBrush)); } }

        private string? _detail;
        public string? Detail { get => _detail; set => SetField(ref _detail, value); }

        public bool IsDepthSlot => Type == MapType.Bump;
        public List<string> DepthOptions { get; } = ["Bump", "Normal", "Depth"];
        private string? _selectedDepthOption = "Bump";
        public string? SelectedDepthOption { get => _selectedDepthOption; set => SetField(ref _selectedDepthOption, value); }

        public WpfBrush ButtonBrush => Assigned != null ? WpfBrushes.LightGreen : WpfBrushes.LightGray;
    }

    public class FolderNode(string path) : NotifyBase
    {
        public string FullPath { get; } = path;
        public string Name => Path.GetFileName(FullPath);

        private ObservableCollection<FolderNode>? _children;
        public ObservableCollection<FolderNode> Children
        {
            get
            {
                if (_children == null)
                    LoadChildren();
                return _children!;
            }
        }

        private void LoadChildren()
        {
            _children = [];
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(FullPath))
                    _children.Add(new FolderNode(dir));
            }
            catch { }
        }

        public void EnsureChildrenLoaded()
        {
            if (_children == null)
                LoadChildren();
        }
    }



    public class ThumbItem : NotifyBase
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string ThumbPath { get; set; } = string.Empty;
    }

    [ValueConversion(typeof(string), typeof(BitmapImage))]
    public class PathToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && File.Exists(path))
            {
                try
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.UriSource = new Uri(path, UriKind.Absolute);
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    img.Freeze();
                    return img;
                }
                catch { /* ignore invalid or locked images */ }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class FolderStorageHandler : IExternalEventHandler
    {
        private Action<UIApplication>? _action;

        public void Request(Action<UIApplication> action)
        {
            _action = action;
            App.FolderEvent?.Raise();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _action?.Invoke(app);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater2026", "Erreur stockage Revit : " + ex.Message);
            }
            finally
            {
                _action = null;
            }
        }

        public string GetName() => "Folder Storage Handler";
    }




    // ================================
    // SERVICES
    // ================================
    public static class FileService
    {
        private static readonly string[] _imgExts =
            [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".webp"];

        public static bool IsImage(string path)
        {
            string ext = Path.GetExtension(path);
            return !string.IsNullOrEmpty(ext) &&
                   _imgExts.Contains(ext.ToLowerInvariant());
        }

        public static string? FindThumb(string folder, int size)
        {
            var baseName = Path.GetFileName(folder);
            var candidate = Path.Combine(folder, $"{baseName}_{size}.jpg");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(folder, $"{baseName}.jpg");
            return File.Exists(candidate) ? candidate : null;
        }
    }

    public static class SettingsStorageService
    {
        private static readonly Guid _schemaGuid = new("AE0A70E5-7A76-4D3D-A5C5-3E82F74C3C6F");
        private const string FieldRootFolder = "RootFolder";

        public static void SaveRootFolder(Document doc, string path)
        {
            if (doc == null || string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                var schema = Schema.Lookup(_schemaGuid);
                if (schema == null)
                {
                    var builder = new SchemaBuilder(_schemaGuid);
                    builder.SetSchemaName("Mater2026Settings");
                    builder.AddSimpleField(FieldRootFolder, typeof(string));
                    builder.SetReadAccessLevel(AccessLevel.Public);
                    builder.SetWriteAccessLevel(AccessLevel.Public);
                    schema = builder.Finish();
                }

                var data = new Entity(schema);
                data.Set(FieldRootFolder, path);

                using var tx = new Transaction(doc, "Save Mater Settings");
                tx.Start();
                doc.ProjectInformation.SetEntity(data);
                tx.Commit();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater2026", $"Erreur stockage Revit : {ex.Message}");
            }
        }

        /// <summary>
        /// Récupère le chemin racine enregistré dans le document.
        /// </summary>
        public static string? LoadRootFolder(Document doc)
        {
            try
            {
                var schema = Schema.Lookup(_schemaGuid);
                if (schema == null) return null;

                var entity = doc.ProjectInformation.GetEntity(schema);
                if (entity.Schema == null) return null;

                return entity.Get<string>(schema.GetField(FieldRootFolder));
            }
            catch
            {
                return null;
            }
        }
    }


    public class MaterialItem(ElementId id, string name) : NotifyBase
    {
        public ElementId Id { get; } = id;
        public string Name { get; } = name;

        private bool _isUsed;
        public bool IsUsed
        {
            get => _isUsed;
            set => SetField(ref _isUsed, value);
        }

        public override string ToString() => Name;
    }


    public static class MapDetectionService
    {
        private static readonly Dictionary<MapType, string[]> _patterns = new()
        {
            { MapType.Albedo, new [] { "diff","dif","albedo","basecolor","col","color","rgb" } },
            { MapType.Bump, new [] { "bump","normal","disp","height","depth" } },
            { MapType.Roughness, new [] { "rough","gloss" } },
            { MapType.Reflection, new [] { "refl","metal" } },
            { MapType.Refraction, new [] { "refr","opac","transp" } },
            { MapType.Illumination, new [] { "emit","glow","light" } }
        };

        public static Dictionary<MapType, MapFile> DetectMaps(string folderPath)
        {
            var result = new Dictionary<MapType, MapFile>();
            if (!Directory.Exists(folderPath)) return result;
            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                var lower = Path.GetFileName(file).ToLowerInvariant();
                foreach (var kv in _patterns)
                {
                    if (kv.Value.Any(p => lower.Contains(p)))
                    {
                        result[kv.Key] = new MapFile(file, kv.Key);
                        break;
                    }
                }
            }
            return result;
        }
    }

    public static class ColorDialogService
    {
        public static Autodesk.Revit.DB.Color? PickColor()
        {
            // Placeholder : si tu as un color picker Revit, mets-le ici.
            return new Autodesk.Revit.DB.Color(255, 255, 255);
        }
    }

    public static class FileBrowserService
    {

        public static string? BrowseForFolder(string? initialDir = null)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choisir un dossier racine des textures",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
                SelectedPath = initialDir ?? ""
            };

            return dialog.ShowDialog() == DialogResult.OK
                ? dialog.SelectedPath
                : null;
        }

        public static string? BrowseForImage(string? baseDir = null)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Choisir une image",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp",
                InitialDirectory = baseDir ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                CheckFileExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog() == DialogResult.OK
                ? dialog.FileName
                : null;
        }
    }

    public static class ToastService
    {
        public static void Show(string msg) =>
            TaskDialog.Show("Mater2026", msg);
    }

    // ================================
    // VIEWMODEL
    // ================================
    public class MaterUiState : NotifyBase
    {
        private string _materialName = "";
        public string MaterialName { get => _materialName; set => SetField(ref _materialName, value); }

        private string _folderPath = "";
        public string FolderPath { get => _folderPath; set => SetField(ref _folderPath, value); }

        private double _widthCm = 300, _heightCm = 300, _rotationDeg = 0;
        public double WidthCm { get => _widthCm; set => SetField(ref _widthCm, value); }
        public double HeightCm { get => _heightCm; set => SetField(ref _heightCm, value); }
        public double RotationDeg { get => _rotationDeg; set => SetField(ref _rotationDeg, value); }

        private int _tilesX = 0, _tilesY = 0;
        public int TilesX { get => _tilesX; set => SetField(ref _tilesX, value); }
        public int TilesY { get => _tilesY; set => SetField(ref _tilesY, value); }
        private bool _tileOffset;
        public bool TileOffset { get => _tileOffset; set => SetField(ref _tileOffset, value); }

        private Autodesk.Revit.DB.Color? _tint;
        public Autodesk.Revit.DB.Color? Tint { get => _tint; set { SetField(ref _tint, value); OnPropertyChanged(nameof(TintBrush)); } }
        public WpfBrush TintBrush => _tint != null ? new SolidColorBrush(WpfColor.FromRgb(_tint.Red, _tint.Green, _tint.Blue)) : WpfBrushes.White;
    }

    public class MaterViewModel : NotifyBase
    {
        private readonly UIDocument _uidoc;
        public MaterUiState Ui { get; } = new();
        public ObservableCollection<MaterialItem> ProjectMaterials { get; } = [];
        public ICollectionView MaterialsView { get; }
        private string _materialFilter = string.Empty;
        public string MaterialFilter
        {
            get => _materialFilter;
            set
            {
                if (SetField(ref _materialFilter, value))
                {
                    // refresh the view whenever the filter text changes
                    MaterialsView.Refresh();
                }
            }
        }
        private MaterialItem? _selectedMaterial;
        public MaterialItem? SelectedMaterial
        {
            get => _selectedMaterial;
            set { SetField(ref _selectedMaterial, value); RefreshButtons(); }
        }

        private bool _showUnused = true;
        public bool ShowUnused
        {
            get => _showUnused;
            set
            {
                if (SetField(ref _showUnused, value))
                {
                    MaterialsView.Refresh(); // refresh the filtered list
                }
            }
        }


        private string _rootFolder = @"C:\ASSETS\Maps";
        public string RootFolder
        {
            get => _rootFolder;
            set
            {
                if (SetField(ref _rootFolder, value))
                {
                    SettingsStorageService.SaveRootFolder(_uidoc.Document, _rootFolder);
                    LoadRootFolders();
                }
            }

        }


        public ObservableCollection<int> ThumbSizes { get; } = new([128, 256, 512, 1024]);
        public RelayCommand GenerateThumbsCmd { get; }

        public ObservableCollection<FolderNode> TreeRoot { get; } = [];
        public ObservableCollection<ThumbItem> GridFolders { get; } = [];
        public ObservableCollection<string> Breadcrumb { get; } = [];

        public ObservableCollection<MapSlot> MapSlots { get; } =
        [
            new MapSlot(MapType.Albedo),
            new MapSlot(MapType.Bump),
            new MapSlot(MapType.Roughness),
            new MapSlot(MapType.Reflection),
            new MapSlot(MapType.Refraction),
            new MapSlot(MapType.Illumination),
        ];

        private string _validationMessage = "";
        public string ValidationMessage { get => _validationMessage; set => SetField(ref _validationMessage, value); }

        private string _actionButtonLabel = "Create";
        public string ActionButtonLabel { get => _actionButtonLabel; set => SetField(ref _actionButtonLabel, value); }

        private bool _isGenerating;
        public bool IsGenerating
        {
            get => _isGenerating;
            set => SetField(ref _isGenerating, value);
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetField(ref _progressValue, value);
        }

        private string _progressText = "";
        public string ProgressText
        {
            get => _progressText;
            set => SetField(ref _progressText, value);
        }
        public RelayCommand ActionButtonCmd { get; }
        public RelayCommand AssignMaterialCmd { get; }
        public RelayCommand PickCmd { get; }

        public RelayCommand ClearFilterCmd { get; }

        public RelayCommand NavigateBreadcrumbCmd { get; }

        private int _thumbSize = 256;
        public int ThumbSize
        {
            get => _thumbSize;
            set
            {
                if (SetField(ref _thumbSize, value))
                {
                    // Déclenche le rafraîchissement asynchrone quand la taille change
                    _ = RefreshThumbnailsAsync();
                }
            }
        }
        // ================================================
        // CHARGEMENT INITIAL DU TREEVIEW
        // ================================================
        public async Task InitializeTreeRootAsync(string baseDir)
        {
            TreeRoot.Clear();

            if (Directory.Exists(baseDir))
            {
                var root = new FolderNode(baseDir);
                TreeRoot.Add(root);
                await Task.CompletedTask;
            }
        }

        public void RefreshBreadcrumb()
        {
            Breadcrumb.Clear();
            if (string.IsNullOrWhiteSpace(Ui.FolderPath)) return;

            string current = "";
            foreach (string part in Ui.FolderPath.Split(Path.DirectorySeparatorChar))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                current = string.IsNullOrEmpty(current) ? part : Path.Combine(current, part);
                Breadcrumb.Add(current);
            }
        }
        public async Task NavigateToFolderAsync(string path)
        {
            if (!Directory.Exists(path))
            {
                ToastService.Show($"Chemin invalide : {path}");
                return;
            }

            Ui.FolderPath = path;
            RefreshBreadcrumb();

            await OnFolderSelectedAsync(path);
        }



        public string? RebuildPathFromBreadcrumb(string segment)
        {
            try
            {
                if (!Breadcrumb.Contains(segment))
                    return null;

                int idx = Breadcrumb.IndexOf(segment);
                string path = string.Join(Path.DirectorySeparatorChar.ToString(),
                                          Breadcrumb.Take(idx + 1));
                return path;
            }
            catch
            {
                return null;
            }
        }


        public FolderNode? FindNodeByPath(string fullPath)
        {
            foreach (var root in TreeRoot)
            {
                var found = FindRecursive(root, fullPath);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static FolderNode? FindRecursive(FolderNode node, string fullPath)
        {
            if (string.Equals(node.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                return node;

            foreach (var child in node.Children)
            {
                var found = FindRecursive(child, fullPath);
                if (found != null)
                    return found;
            }

            return null;
        }


        public bool IsValid => string.IsNullOrWhiteSpace(ValidationMessage);

        public RelayCommand BrowseRootCmd { get; }
        public MaterViewModel(UIDocument uidoc)
        {
            _uidoc = uidoc;
            ActionButtonCmd = new RelayCommand(_ => OnActionButton());
            AssignMaterialCmd = new RelayCommand(_ => OnAssign(), _ => SelectedMaterial != null);
            PickCmd = new RelayCommand(_ => OnPickMaterial());
            ClearFilterCmd = new RelayCommand(_ => MaterialFilter = string.Empty);
            NavigateBreadcrumbCmd = new RelayCommand(p => OnNavigateBreadcrumb(p?.ToString() ?? ""));
            MaterialsView = CollectionViewSource.GetDefaultView(ProjectMaterials);
            GenerateThumbsCmd = new RelayCommand(_ => GenerateThumbs());
            MaterialsView.Filter = MaterialFilterPredicate;
            ShowUnused = true;
            BrowseRootCmd = new RelayCommand(_ => BrowseRootFolder());

            // 1️⃣ Charge la liste des matériaux du projet
            LoadProjectMaterials();

            // 2️⃣ Recharge le dossier racine enregistré (Extensible Storage Revit)
            try
            {
                string? saved = RevitStorageService.LoadString(_uidoc.Document, "RootFolder");
                if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
                {
                    RootFolder = saved;
                }
                else
                {
                    RootFolder = @"C:\ASSETS\Maps";
                }
            }
            catch
            {
                RootFolder = @"C:\ASSETS\Maps";
            }

            // 3️⃣ Charge l’arborescence après la construction complète
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LoadRootFolders();
            });


        }

        private void BrowseRootFolder()
        {
            try
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select Root folder for PBR",
                    SelectedPath = Directory.Exists(RootFolder)
                        ? RootFolder
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    ShowNewFolderButton = false
                };

                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                RootFolder = dlg.SelectedPath;

                ToastService.Show($"Dossier racine défini : {RootFolder}");

                // 🔹 Schedule the Revit storage update via ExternalEvent
                if (App.FolderHandler == null || App.FolderEvent == null)
                {
                    ToastService.Show("Folder handler not initialized. Restart Revit.");
                    return;
                }

                App.FolderHandler.Request(app =>
                {
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null) return;

                    using var tx = new Transaction(doc, "Mater2026 - Save Root Folder");
                    tx.Start();
                    RevitStorageService.SaveString(doc, "RootFolder", RootFolder);
                    tx.Commit();
                });

                // ✅ Reload the tree view after update (pure WPF side)
                LoadRootFolders();
            }
            catch (Exception ex)
            {
                ToastService.Show($"Erreur lors de la sélection du dossier : {ex.Message}");
            }
        }
        public static class RevitStorageService
{
    private static readonly Guid SchemaGuid = new("6A9A93B9-FE47-4D69-B548-FB9382C9F7BA");

    public static void SaveString(Document doc, string fieldName, string value)
    {
        var schema = Schema.Lookup(SchemaGuid) ?? CreateSchema();
        var entity = doc.ProjectInformation.GetEntity(schema);
        if (!entity.IsValid())
            entity = new Entity(schema);

        entity.Set(schema.GetField(fieldName), value);
        doc.ProjectInformation.SetEntity(entity);
    }

    public static string? LoadString(Document doc, string fieldName)
    {
        var schema = Schema.Lookup(SchemaGuid);
        if (schema == null) return null;

        var entity = doc.ProjectInformation.GetEntity(schema);
        if (!entity.IsValid()) return null;

        return entity.Get<string>(schema.GetField(fieldName));
    }

    private static Schema CreateSchema()
    {
        var builder = new SchemaBuilder(SchemaGuid);
        builder.SetSchemaName("Mater2026Settings");
        builder.AddSimpleField("RootFolder", typeof(string));
        return builder.Finish();
    }
}


        private bool MaterialFilterPredicate(object obj)
        {
            if (obj is not MaterialItem m) return false;

            bool matchesText = string.IsNullOrWhiteSpace(MaterialFilter)
                || m.Name.Contains(MaterialFilter, StringComparison.CurrentCultureIgnoreCase);

            // si ShowUnused est vrai → affiche tout (filtre texte seulement)
            if (ShowUnused)
                return matchesText;

            // sinon → affiche uniquement ceux utilisés
            return m.IsUsed && matchesText;
        }

        private async void GenerateThumbs()
        {
            // 1️⃣ Vérifie que le dossier racine est défini
            if (string.IsNullOrWhiteSpace(Ui.FolderPath) || !Directory.Exists(Ui.FolderPath))
            {
                ToastService.Show("Aucun dossier sélectionné.");
                return;
            }

            // 2️⃣ Récupère les sous-dossiers visibles dans la grille
            var visibleFolders = GridFolders.ToList();
            if (visibleFolders.Count == 0)
            {
                ToastService.Show("Aucun dossier visible à traiter.");
                return;
            }

            int size = ThumbSize;
            ToastService.Show($"Génération des miniatures visibles ({size}px)...");

            IsGenerating = true;
            ProgressValue = 0;
            ProgressText = "Préparation...";

            await Task.Run(() =>
            {
                int total = visibleFolders.Count;
                int count = 0;

                foreach (var item in visibleFolders)
                {
                    try
                    {
                        string folder = item.FullPath;
                        if (!Directory.Exists(folder))
                            continue;

                        string baseName = Path.GetFileName(folder);
                        string? refImage = null;

                        // 1️⃣ Cherche une image qui correspond au nom du dossier
                        refImage = Directory.EnumerateFiles(folder)
                            .FirstOrDefault(f =>
                            {
                                string name = Path.GetFileNameWithoutExtension(f);
                                return string.Equals(name, baseName, StringComparison.OrdinalIgnoreCase)
                                    && FileService.IsImage(f);
                            });

                        // 2️⃣ Sinon, cherche une image contenant "thumb", "thumbnail", "preview" ou "render"
                        if (refImage == null)
                        {
                            string[] keywords = ["thumb", "thumbnail", "preview", "render"];
                            refImage = Directory.EnumerateFiles(folder)
                                .FirstOrDefault(f =>
                                    FileService.IsImage(f) &&
                                    keywords.Any(k =>
                                        Path.GetFileNameWithoutExtension(f)
                                            .Contains(k, StringComparison.OrdinalIgnoreCase)));
                        }

                        // 3️⃣ Sinon, prends la première image disponible
                        refImage ??= Directory.EnumerateFiles(folder)
                            .FirstOrDefault(FileService.IsImage);

                        // 4️⃣ Si toujours rien → on saute le dossier
                        if (refImage == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ Aucune image trouvée pour {folder}");
                            continue;
                        }

                        // 🔹 Destination = même dossier, suffixe = taille
                        string dest = Path.Combine(folder, $"{baseName}_{size}.jpg");

                        // 🔹 Ignore si déjà à jour
                        if (File.Exists(dest) && File.GetLastWriteTime(dest) > File.GetLastWriteTime(refImage))
                        {
                            System.Diagnostics.Debug.WriteLine($"⏭️ Déjà à jour : {dest}");
                            continue;
                        }

                        // 🔹 Lecture + redimensionnement
                        byte[] data = File.ReadAllBytes(refImage);
                        using var ms = new MemoryStream(data);

                        var src = new BitmapImage();
                        src.BeginInit();
                        src.StreamSource = ms;
                        src.DecodePixelWidth = size;
                        src.CacheOption = BitmapCacheOption.OnLoad;
                        src.EndInit();
                        src.Freeze();

                        // 🔹 Sauvegarde JPEG
                        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                        encoder.Frames.Add(BitmapFrame.Create(src));

                        using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.Read))
                            encoder.Save(fs);

                        // 🔹 Mise à jour UI immédiate
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            item.ThumbPath = dest;
                        });

                        System.Diagnostics.Debug.WriteLine($"✅ Miniature générée : {dest}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Erreur miniature : {ex.Message}");
                    }

                    count++;
                    double progress = (double)count / total * 100.0;

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressValue = progress;
                        ProgressText = $"{count}/{total} dossiers traités";
                    });
                }
            });

            IsGenerating = false;
            ToastService.Show("Miniatures visibles générées !");
        }





        private void LoadRootFolders()
        {
            TreeRoot.Clear();

            if (!Directory.Exists(RootFolder))
            {
                ToastService.Show($"Dossier racine introuvable : {RootFolder}");
                return;
            }

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(RootFolder))
                    TreeRoot.Add(new FolderNode(dir));
            }
            catch (Exception ex)
            {
                ToastService.Show($"Erreur lecture dossiers : {ex.Message}");
            }
        }


        private static HashSet<ElementId> GetUsedMaterialIds(Document doc)
        {
            var used = new HashSet<ElementId>();

            // 1️⃣ Matériaux par catégorie
            foreach (var cat in doc.Settings.Categories.Cast<Category>())
            {
                if (cat.Material != null)
                    used.Add(cat.Material.Id);
            }

            // 2️⃣ Matériaux présents sur les instances
            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            foreach (var elem in collector)
            {
                // Paramètres de type Material
                foreach (Parameter p in elem.Parameters)
                {
                    if (p.StorageType == StorageType.ElementId)
                    {
                        ElementId id = p.AsElementId();
                        if (id != ElementId.InvalidElementId && doc.GetElement(id) is Material)
                            used.Add(id);
                    }
                }

                // 3️⃣ Matériaux peints sur les faces
                var geo = elem.get_Geometry(new Options());
                if (geo == null) continue;

                foreach (GeometryObject obj in geo)
                {
                    if (obj is Solid solid)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            ElementId paintId = doc.GetPaintedMaterial(elem.Id, face);
                            if (paintId != ElementId.InvalidElementId)
                                used.Add(paintId);
                        }
                    }
                }
            }

            return used;
        }

        private void LoadProjectMaterials()
        {
            ProjectMaterials.Clear();
            var doc = _uidoc.Document;
            var mats = RevitMaterialService.GetProjectMaterials(_uidoc);

            var usedIds = GetUsedMaterialIds(doc); // ✅ ici on l'appelle

            foreach (var (id, name) in mats)
            {
                var item = new MaterialItem(id, name)
                {
                    IsUsed = usedIds.Contains(id)
                };
                ProjectMaterials.Add(item);
            }

            MaterialsView.Refresh();
            MaterialsView.SortDescriptions.Clear();
            MaterialsView.SortDescriptions.Add(new SortDescription(nameof(MaterialItem.Name), ListSortDirection.Ascending));

        }

        private void ResetUiState()
        {
            Ui.MaterialName = "";
            Ui.WidthCm = 300;
            Ui.HeightCm = 300;
            Ui.RotationDeg = 0;
            Ui.TilesX = 0;
            Ui.TilesY = 0;
            Ui.TileOffset = false;
            Ui.Tint = null;

            foreach (var slot in MapSlots)
            {
                slot.Assigned = null;
                slot.Detail = null;
                slot.SelectedDepthOption = "Bump";
            }
        }


        private void OnPickMaterial()
        {
            // 1️⃣ Cache temporairement la fenêtre pour redonner la main à Revit
            foreach (System.Windows.Window win in System.Windows.Application.Current.Windows)
            {
                if (win is MaterWindow)
                {
                    win.Hide();
                    break;
                }
            }

            // 2️⃣ Vérifie que l’ExternalEvent a bien été créé (à OnStartup)
            if (App.PickHandler == null || App.PickEvent == null)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Mater2026", "Pick handler not initialized. Restart Revit to re-load the add-in.");
                return;
            }

            // 3️⃣ Lance la requête (non bloquante)
            App.PickHandler.Request(_uidoc, matId =>
            {
                // 4️⃣ Quand Revit a fini le pick, ré-affiche la fenêtre
                foreach (System.Windows.Window win in System.Windows.Application.Current.Windows)
                {
                    if (win is MaterWindow)
                    {
                        win.Show();
                        win.Activate();
                        break;
                    }
                }

                // 5️⃣ Si rien n’a été sélectionné, on arrête là
                if (matId == null || matId == ElementId.InvalidElementId)
                    return;

                // 6️⃣ Sélectionne automatiquement le matériau correspondant
                var item = ProjectMaterials.FirstOrDefault(m => m.Id == matId);
                if (item != null)
                {
                    SelectedMaterial = item;

                    // (facultatif) met un petit retour visuel dans Revit
                    try
                    {
                        using var mat = _uidoc.Document.GetElement(matId) as Autodesk.Revit.DB.Material;
                        if (mat != null)
                            ToastService.Show($"Matériau sélectionné : {mat.Name}");
                    }
                    catch { /* optionnel, silencieux */ }
                }
                else
                {
                    ToastService.Show("Aucun matériau correspondant trouvé dans la liste du projet.");
                }
            });
        }
        public async Task RefreshThumbnailsAsync()
        {
            string folder = Ui.FolderPath;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            // Petite pause pour éviter les rafraîchissements multiples si on change vite la taille
            await Task.Delay(150);

            var thumbs = await Task.Run(() =>
            {
                var result = new List<ThumbItem>();
                foreach (var dir in Directory.EnumerateDirectories(folder))
                {
                    result.Add(new ThumbItem
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        ThumbPath = FileService.FindThumb(dir, ThumbSize)
                                   ?? FileService.FindThumb(dir, 0)
                                   ?? ""
                    });
                }
                return result;
            });

            // Mise à jour sur le thread UI
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                GridFolders.Clear();
                foreach (var t in thumbs)
                    GridFolders.Add(t);
            });

        }

        public async Task OnFolderSelectedAsync(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            ResetUiState();
            Ui.FolderPath = folder;

            Breadcrumb.Clear();
            foreach (var p in folder.Split(Path.DirectorySeparatorChar))
                Breadcrumb.Add(p);

            GridFolders.Clear();

            var subFolders = await Task.Run(() =>
            {
                try
                {
                    return [.. Directory.EnumerateDirectories(folder)
                        .Select(dir => new ThumbItem
                        {
                            Name = Path.GetFileName(dir),
                            FullPath = dir,
                            ThumbPath = (FileService.FindThumb(dir, ThumbSize)
                               ?? FileService.FindThumb(dir, 0)
                               ?? string.Empty)!
                        })];
                }
                catch
                {
                    return new List<ThumbItem>();
                }
            });

            foreach (var item in subFolders)
                GridFolders.Add(item);

            if (subFolders.Count == 0)
            {
                var maps = await Task.Run(() => MapDetectionService.DetectMaps(folder));
                foreach (var slot in MapSlots)
                    slot.Assigned = maps.TryGetValue(slot.Type, out var mf) ? mf : null;

                Ui.MaterialName = Path.GetFileName(folder);
            }
        }



        private void RefreshButtons()
        {
            if (!string.IsNullOrEmpty(Ui.MaterialName))
            {
                var doc = _uidoc.Document;
                var existing = new FilteredElementCollector(doc)
                    .OfClass(typeof(AppearanceAssetElement))
                    .Cast<AppearanceAssetElement>()
                    .FirstOrDefault(a => a.Name.Equals(Ui.MaterialName, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    ActionButtonLabel = SelectedMaterial != null ? "Update" : "Update Existing";
                }
                else
                {
                    ActionButtonLabel = SelectedMaterial != null ? "Replace" : "Create";
                }
            }
        }

        private void OnActionButton()
        {
            if (!IsValid)
            {
                ToastService.Show("Invalid inputs");
                return;
            }

            if (App.ApplyHandler == null || App.ApplyEvent == null)
            {
                ToastService.Show("Apply handler not initialized. Restart Revit.");
                return;
            }

            // 🔹 Send Revit-safe work to be executed inside API context
            App.ApplyHandler.Request(app =>
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc.Document;

                var appElem = RevitMaterialService.GetOrCreateAppearanceByFolder(doc, Ui.MaterialName);

                var mapDict = new Dictionary<MapType, (string? path, bool invert, string? detail)>();
                foreach (var s in MapSlots)
                {
                    if (s.Assigned != null)
                        mapDict[s.Type] = (s.Assigned.FullPath, false, s.Detail);
                }

                RevitMaterialService.ApplyUiToAppearance(
                    appElem,
                    mapDict,
                    Ui.WidthCm,
                    Ui.HeightCm,
                    Ui.RotationDeg,
                    Ui.Tint
                );

                Autodesk.Revit.DB.Material? targetMat = null;

                if (SelectedMaterial != null)
                {
                    var (mat, _) = RevitMaterialService.GetMaterialAndAppearance(doc, SelectedMaterial.Id);
                    if (mat is not null)
                    {
                        RevitMaterialService.ReplaceMaterialAppearance(mat, appElem);
                        targetMat = mat;
                    }
                }
                else
                {
                    targetMat = RevitMaterialService.CreateMaterial(doc, Ui.MaterialName, appElem);
                    LoadProjectMaterials(); // this stays safe (it just reads)
                }

                if (targetMat != null)
                {
                    TilePatternService.ApplyTilesToMaterial(
                        doc,
                        targetMat,
                        Ui.WidthCm,
                        Ui.HeightCm,
                        Ui.TilesX,
                        Ui.TilesY,
                        Ui.TileOffset
                    );
                }
            });
        }


        private static void OnAssign() => ToastService.Show("Assign material to faces (TODO)");
        private static void OnNavigateBreadcrumb(string path) => ToastService.Show($"Navigate {path}");
    }

    // ================================
    // REVIT MATERIAL SERVICE
    // ================================
    public static partial class RevitMaterialService
    {
        [GeneratedRegex(@"tiles_(\d+)_\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex TilesRegex();

        public static IList<(ElementId Id, string Name)> GetProjectMaterials(UIDocument uidoc)
        {
            var doc = uidoc.Document;
            var mats = new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Material)).Cast<Autodesk.Revit.DB.Material>()
                .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase);
            return [.. mats.Select(m => (m.Id, m.Name))];
        }

        public static (Autodesk.Revit.DB.Material? Mat, AppearanceAssetElement? App) GetMaterialAndAppearance(Document doc, ElementId id)
        {
            var mat = doc.GetElement(id) as Autodesk.Revit.DB.Material;
            AppearanceAssetElement? app = null;
            if (mat != null && mat.AppearanceAssetId != ElementId.InvalidElementId)
                app = doc.GetElement(mat.AppearanceAssetId) as AppearanceAssetElement;
            return (mat, app);
        }

        public static AppearanceAssetElement GetOrCreateAppearanceByFolder(Document doc, string folderName)
        {
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>()
                .FirstOrDefault(a => a.Name.Equals(folderName, StringComparison.CurrentCultureIgnoreCase));
            if (existing != null) return existing;

            var baseApp = new FilteredElementCollector(doc)
                .OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>()
                .FirstOrDefault(a => a.Name == "Generic")
                ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(AppearanceAssetElement))
                    .Cast<AppearanceAssetElement>()
                    .First();

            using var tx = new Transaction(doc, "Create Appearance");
            tx.Start();
            var dup = baseApp.Duplicate(folderName);
            tx.Commit();
            return dup;
        }

        public static void ApplyUiToAppearance(
            AppearanceAssetElement app,
            IDictionary<MapType, (string? path, bool invert, string? detail)> maps,
            double widthCm, double heightCm, double rotationDeg,
            Autodesk.Revit.DB.Color? tint)
        {
            var doc = app.Document;
            using var tx = new Transaction(doc, "Apply Appearance");
            tx.Start();
            using (var scope = new AppearanceAssetEditScope(doc))
            {
                var editable = scope.Start(app.Id);

                void SetSlot(string slotName, (string? path, bool invert, string? detail) val)
                {
                    if (string.IsNullOrWhiteSpace(val.path)) return;
                    var prop = editable.FindByName(slotName) as AssetProperty;
                    var ub = prop?.GetSingleConnectedAsset();
                    if (ub == null) return;

                    if (ub.FindByName("unifiedbitmap_Bitmap") is AssetPropertyString bmp && !bmp.IsReadOnly)
                        bmp.Value = val.path;

                    if (ub.FindByName("unifiedbitmap_RealWorldScaleX") is AssetPropertyDouble sx && !sx.IsReadOnly)
                        sx.Value = widthCm / 100.0;
                    if (ub.FindByName("unifiedbitmap_RealWorldScaleY") is AssetPropertyDouble sy && !sy.IsReadOnly)
                        sy.Value = heightCm / 100.0;

                    if (ub.FindByName("unifiedbitmap_WAngle") is AssetPropertyDouble rot && !rot.IsReadOnly)
                        rot.Value = rotationDeg;

                    if (tint != null && ub.FindByName("common_Tint_color") is AssetPropertyDoubleArray4d col && !col.IsReadOnly)
                        col.SetValueAsDoubles([tint.Red / 255.0, tint.Green / 255.0, tint.Blue / 255.0, 1]);
                }

                if (maps.TryGetValue(MapType.Albedo, out var alb)) SetSlot("generic_diffuse", alb);
                if (maps.TryGetValue(MapType.Bump, out var bump)) SetSlot("generic_bump_map", bump);
                if (maps.TryGetValue(MapType.Roughness, out var rough)) SetSlot("generic_roughness", rough);
                if (maps.TryGetValue(MapType.Reflection, out var refl)) SetSlot("generic_reflectivity_at_0deg", refl);
                if (maps.TryGetValue(MapType.Refraction, out var refr)) SetSlot("generic_transparency", refr);
                if (maps.TryGetValue(MapType.Illumination, out var emis)) SetSlot("generic_emission_color", emis);

                scope.Commit(true);
            }
            tx.Commit();
        }

        public static Autodesk.Revit.DB.Material CreateMaterial(Document doc, string name, AppearanceAssetElement app)
        {
            using var tx = new Transaction(doc, "Create Material");
            tx.Start();
            var mid = Autodesk.Revit.DB.Material.Create(doc, name);
            var mat = (Autodesk.Revit.DB.Material)doc.GetElement(mid);
            mat.AppearanceAssetId = app.Id;
            tx.Commit();
            return mat;
        }

        public static void ReplaceMaterialAppearance(Autodesk.Revit.DB.Material mat, AppearanceAssetElement app)
        {
            using var tx = new Transaction(mat.Document, "Replace Appearance");
            tx.Start();
            mat.AppearanceAssetId = app.Id;
            tx.Commit();
        }
    }

    // ================================
    // TILE PATTERN (fallback sûr)
    // ================================
    public static class TilePatternService
    {
        /// <summary>
        /// Applique (ou retire) un motif de tuiles au matériau. 
        /// Fallback: crée un motif modèle minimal si absent (pas d'espacement paramétrique ici, à cause des API non exposées).
        /// </summary>
        public static void ApplyTilesToMaterial(
            Document doc,
            Autodesk.Revit.DB.Material mat,
            double widthCm,
            double heightCm,
            int tilesX,
            int tilesY,
            bool offsetHalfTile)
        {
            // 0/0 => on retire tout motif
            if (tilesX <= 0 && tilesY <= 0)
            {
                using var tx0 = new Transaction(doc, "Remove Pattern");
                tx0.Start();
                mat.SurfaceForegroundPatternId = ElementId.InvalidElementId;
                tx0.Commit();
                return;
            }

            // Nom logique (on garde ta convention, utile si tu veux livrer des patterns depuis un gabarit)
            string name = $"tiles_{tilesX}_{tilesY}" + (offsetHalfTile ? "_offset" : "");

            using var tx = new Transaction(doc, "Apply Tile Pattern");
            tx.Start();

            // 1) Existe déjà ?
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                mat.SurfaceForegroundPatternId = existing.Id;
                tx.Commit();
                return;
            }

            // 2) Fallback : créer un motif modèle minimal (Revit génère une grille interne par défaut)
            //    NOTE : Ici on ne peut PAS fixer l’espacement fin (API non dispo dans ta build).
            var fp = new FillPattern(name, FillPatternTarget.Model, FillPatternHostOrientation.ToHost);
            var fpe = FillPatternElement.Create(doc, fp);

            mat.SurfaceForegroundPatternId = fpe.Id;
            tx.Commit();
        }
    }
    public class PickMaterialHandler : IExternalEventHandler
    {
        private UIDocument? _uidoc;
        private Action<ElementId?>? _callback;

        /// <summary>
        /// Lance une requête de sélection (appelée depuis ton ViewModel).
        /// </summary>
        public void Request(UIDocument uidoc, Action<ElementId?> callback)
        {
            _uidoc = uidoc;
            _callback = callback;
            App.PickEvent?.Raise(); // déclenche l’exécution Revit-safe
        }

        public void Execute(UIApplication app)
        {
            try
            {
                UIDocument uidoc = app.ActiveUIDocument;
                if (uidoc == null)
                {
                    _callback?.Invoke(null);
                    return;
                }

                Document doc = uidoc.Document;

                // ✅ laisse l'utilisateur cliquer sur une face
                Reference r = uidoc.Selection.PickObject(ObjectType.Face, "Pick a face or element to get its material");
                if (r == null)
                {
                    _callback?.Invoke(null);
                    return;
                }

                Element elem = doc.GetElement(r);
                if (elem == null)
                {
                    _callback?.Invoke(null);
                    return;
                }

                GeometryObject geoObj = elem.GetGeometryObjectFromReference(r);
                ElementId? matId = null;

                // 🔹 1) Matériau peint
                if (geoObj is Face face)
                {
                    ElementId paintedId = doc.GetPaintedMaterial(elem.Id, face);
                    if (paintedId != ElementId.InvalidElementId)
                        matId = paintedId;
                    else
                    {
                        // 🔹 2) Matériau de la face
                        ElementId baseId = face.MaterialElementId;
                        if (baseId != ElementId.InvalidElementId)
                            matId = baseId;
                    }
                }

                // 🔹 3) Fallback : matériau de l’élément
                if (matId == null || matId == ElementId.InvalidElementId)
                {
                    var matParam = elem.Parameters
                        .Cast<Parameter>()
                        .FirstOrDefault(p =>
                            p.StorageType == StorageType.ElementId &&
                            (p.Definition.Name.Contains("Matériau", StringComparison.OrdinalIgnoreCase) ||
                             p.Definition.Name.Contains("Material", StringComparison.OrdinalIgnoreCase)));

                    if (matParam != null)
                    {
                        ElementId id = matParam.AsElementId();
                        if (id != ElementId.InvalidElementId)
                            matId = id;
                    }
                }

                // ✅ Retourne le résultat au callback
                _callback?.Invoke(matId);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                _callback?.Invoke(null);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater2026", "Erreur PickMaterialHandler : " + ex.Message);
                _callback?.Invoke(null);
            }
        }

        public string GetName() => "Pick Material Handler";
    }

    public class ApplyMaterialHandler : IExternalEventHandler
    {
        private Action<UIApplication>? _action;

        public void Request(Action<UIApplication> action)
        {
            _action = action;
            App.ApplyEvent?.Raise();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _action?.Invoke(app);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater2026", "Erreur ApplyMaterialHandler : " + ex.Message);
            }
            finally
            {
                _action = null;
            }
        }

        public string GetName() => "Apply Material Handler";
    }




}
