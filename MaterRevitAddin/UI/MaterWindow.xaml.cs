using Autodesk.Revit.UI;
using Mater2026.Models;
using Mater2026.Services;
using Mater2026.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
// Aliases to avoid ambiguity with WinForms and Revit
using WinForms = System.Windows.Forms;
using SControls = System.Windows.Controls;

// WPF event-args aliases (avoid clash with WinForms)
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfDragEventArgs = System.Windows.DragEventArgs;

namespace Mater2026.UI
{
    public partial class MaterWindow : Window
    {
        public MaterViewModel VM { get; }
        private FileSystemWatcher? _watcher;

        // Lazy-tree: single stub marker (non-visual object is OK for TreeViewItem.Items)
        private static readonly object StubNode = new();

        // drag source anchor (for file tiles)
        private System.Windows.Point _dragStart;

        public MaterWindow(UIApplication uiapp)
        {
            InitializeComponent();
            VM = new MaterViewModel(uiapp);
            VM.AppHide = () => Dispatcher.Invoke(Hide);
            VM.AppShow = () => Dispatcher.Invoke(Show);
            DataContext = VM;

            // Bind lists
            MatList.ItemsSource = VM.ProjectMaterials;  // XAML: DisplayMemberPath="Item2", SelectedValuePath="Item1"
            MapTypesList.ItemsSource = VM.MapTypes;

            // Show/hide from VM
            VM.AppHide = () => Hide();
            VM.AppShow = () => { Show(); Activate(); };

            // Mirror Params -> UI fields (only when VM changes)
            VM.Params.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(UiParameters.FolderPath): FolderBox.Text = VM.Params.FolderPath; break;
                    case nameof(UiParameters.WidthCm): WidthBox.Text = VM.Params.WidthCm.ToString("0.###"); break;
                    case nameof(UiParameters.HeightCm): HeightBox.Text = VM.Params.HeightCm.ToString("0.###"); break;
                    case nameof(UiParameters.RotationDeg): RotBox.Text = VM.Params.RotationDeg.ToString("0.###"); break;
                    case nameof(UiParameters.TilesX): TilesXBox.Text = VM.Params.TilesX.ToString(); break;
                    case nameof(UiParameters.TilesY): TilesYBox.Text = VM.Params.TilesY.ToString(); break;
                }
            };

            // Close => just hide (so you can reopen from the ribbon)
            Closing += (o, e) => { e.Cancel = true; Hide(); };

            // Window-level context menu on the Tree
            var cm = new SControls.ContextMenu();
            var miOpen = new SControls.MenuItem { Header = "Ouvrir dans l’explorateur" };
            miOpen.Click += (_, __) =>
            {
                if (Tree.SelectedItem is SControls.TreeViewItem tvi && tvi.Tag is string path && Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = path,
                        UseShellExecute = true
                    });
                }
            };
            cm.Items.Add(miOpen);
            Tree.ContextMenu = cm;

            // Right-click selects the node first
            Tree.PreviewMouseRightButtonDown += (s, e) =>
            {
                var dep = (DependencyObject)e.OriginalSource;
                while (dep != null && dep is not SControls.TreeViewItem)
                    dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
                if (dep is SControls.TreeViewItem tvi) tvi.IsSelected = true;
            };

            // Restore last root + default thumb size (128)
            Loaded += async (_, __) =>
            {
                EnsureThumbDropDefaults();

                var last = SettingsService.LoadLastRoot();
                if (!string.IsNullOrWhiteSpace(last) && Directory.Exists(last))
                {
                    RootBox.Text = last;
                    await BuildTreeAsync(last);        // show only children (not root)
                    await SelectTreeNodeAsync(last);   // populate center grid with root’s direct children
                }
            };
        }

        private void ToggleGallery_Click(object sender, RoutedEventArgs e) => VM.IsGallery = !VM.IsGallery;

        // ===== Root picker =====
        private async void BtnPickRoot_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new WinForms.FolderBrowserDialog { Description = "Choisir la racine pour le TreeView" };
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                RootBox.Text = dlg.SelectedPath;
                SettingsService.SaveLastRoot(dlg.SelectedPath);
                await BuildTreeAsync(dlg.SelectedPath);
                await SelectTreeNodeAsync(dlg.SelectedPath);
            }
        }

        // ===== Tree (lazy/async), show only folders that themselves have subfolders; DO NOT show the root node =====
        private async Task BuildTreeAsync(string rootPath)
        {
            Tree.Items.Clear();

            string[] firstLevel;
            try { firstLevel = await Task.Run(() => Directory.EnumerateDirectories(rootPath).ToArray()); }
            catch { firstLevel = []; }

            foreach (var sub in firstLevel)
            {
                if (!HasSubdirectory(sub)) continue; // only nodes that have children

                var tvi = new SControls.TreeViewItem
                {
                    Header = Path.GetFileName(sub),
                    Tag = sub
                };
                AttachContextMenu(tvi);
                AddLazyStub(tvi);
                tvi.Expanded += TreeItem_Expanded;
                Tree.Items.Add(tvi);
            }
        }

        private static bool HasSubdirectory(string dir)
        {
            try { return Directory.EnumerateDirectories(dir).Any(); }
            catch { return false; }
        }

        private static void AddLazyStub(SControls.TreeViewItem item)
        {
            item.Items.Clear();
            item.Items.Add(StubNode); // single stub object (not a UI element)
        }

        private async void TreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            var node = (SControls.TreeViewItem)sender;

            // Only populate if we still have the stub
            if (node.Items.Count == 1 && ReferenceEquals(node.Items[0], StubNode))
            {
                node.Items.Clear();

                var path = node.Tag as string;
                if (string.IsNullOrEmpty(path)) return;

                string[] subs;
                try { subs = await Task.Run(() => Directory.EnumerateDirectories(path).ToArray()); }
                catch { return; }

                foreach (var sub in subs)
                {
                    if (!HasSubdirectory(sub)) continue;

                    var child = new SControls.TreeViewItem
                    {
                        Header = Path.GetFileName(sub),
                        Tag = sub
                    };
                    AttachContextMenu(child);
                    AddLazyStub(child);
                    child.Expanded += TreeItem_Expanded;
                    node.Items.Add(child);
                }
            }
        }

        private async void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Tree.SelectedItem is SControls.TreeViewItem tvi && tvi.Tag is string path && Directory.Exists(path))
            {
                await SelectTreeNodeAsync(path);
            }
        }

        private static void AttachContextMenu(SControls.TreeViewItem tvi)
        {
            // Right-click also selects the node
            tvi.PreviewMouseRightButtonDown += (s, e) => { tvi.IsSelected = true; };

            var cm = new SControls.ContextMenu();
            var mi = new SControls.MenuItem { Header = "Ouvrir dans l'explorateur" };
            mi.Click += (_, __) =>
            {
                if (tvi.Tag is string path && Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = path,
                        UseShellExecute = true
                    });
                }
            };
            cm.Items.Add(mi);
            tvi.ContextMenu = cm;
        }

        private async Task SelectTreeNodeAsync(string path)
        {
            // Populate grid with direct children (VM handles leaf/non-leaf thumbs and detection)
            VM.SelectTreeNode(path);
            AttachWatcher(path);           // auto-refresh thumbs state
            RecheckGenerateButtonState();
            await Task.CompletedTask;
        }

        // ===== Watcher: auto-refresh thumbs & Generate button =====
        private void AttachWatcher(string path)
        {
            try
            {
                _watcher?.Dispose();
                _watcher = new FileSystemWatcher(path, "*.jpg")
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _watcher.Created += (_, __) => Dispatcher.Invoke(RefreshThumbsAndButton);
                _watcher.Changed += (_, __) => Dispatcher.Invoke(RefreshThumbsAndButton);
                _watcher.Renamed += (_, __) => Dispatcher.Invoke(RefreshThumbsAndButton);
                _watcher.EnableRaisingEvents = true;
            }
            catch { /* ignore */ }
        }

        private void RefreshThumbsAndButton()
        {
            foreach (var f in VM.GridFolders) f.TouchThumb();
            RecheckGenerateButtonState();
        }

        // ===== Grid (folders) behavior =====
        private async void GridFoldersPanel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var fe = e.OriginalSource as FrameworkElement;
            while (fe != null && fe.DataContext is not FolderItem)
                fe = System.Windows.Media.VisualTreeHelper.GetParent(fe) as FrameworkElement;

            if (fe?.DataContext is FolderItem folder)
            {
                if (HasSubdirectory(folder.FullPath))
                {
                    // Non-leaf: navigate deeper (and sync Tree selection)
                    await SelectTreeNodeAsync(folder.FullPath);
                    SelectTreeNodeInTree(folder.FullPath);
                }
                else
                {
                    // Leaf: detect maps + set standard params
                    try
                    {
                        await VM.DetectMapsForFolderAsync(folder.FullPath);
                        SetStandardParamsAfterDetection(folder.FullPath);
                    }
                    catch (Exception ex)
                    {
                        ToastService.Show("Échec détection: " + ex.Message);
                    }
                }
            }
        }

        private void SelectTreeNodeInTree(string path)
        {
            foreach (var obj in Tree.Items)
                if (obj is SControls.TreeViewItem it && TrySelectRecursive(it, path)) return;
        }

        private static bool TrySelectRecursive(SControls.TreeViewItem node, string path)
        {
            if ((node.Tag as string) == path) { node.IsSelected = true; node.IsExpanded = true; return true; }
            foreach (var c in node.Items.OfType<SControls.TreeViewItem>())
                if (TrySelectRecursive(c, path)) { node.IsExpanded = true; return true; }
            return false;
        }

        // ===== Thumbnail size (dropdown) =====
        private void EnsureThumbDropDefaults()
        {
            ThumbSizeDrop.Items.Clear();
            ThumbSizeDrop.Items.Add(128);
            ThumbSizeDrop.Items.Add(512);
            ThumbSizeDrop.Items.Add(1024);

            ThumbSizeDrop.SelectedItem = 128; // default smallest
            VM.ThumbSize = 128;
        }

        private void ThumbSizeDrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThumbSizeDrop.SelectedItem is int size)
            {
                // Do NOT generate here; just switch to an existing size or fallback to base
                VM.ThumbSize = size;
                foreach (var f in VM.GridFolders) { f.ThumbSize = size; f.TouchThumb(); }
                RecheckGenerateButtonState();
            }
        }

        // ===== Generate missing thumbs (direct children only; never auto) =====
        private async void GenMissing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var root = VM.CurrentGridRoot;
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;

                // only direct children, and only missing sizes
                await VM.GenerateMissingThumbsForDirectChildrenAsync(root, [128, 512, 1024]);
                RefreshThumbsAndButton();
                ToastService.Show("Vignettes générées (manquantes uniquement).");
            }
            catch (Exception ex)
            {
                ToastService.Show("Génération vignettes: " + ex.Message);
            }
        }

        private void RecheckGenerateButtonState()
        {
            var need = VM.GridFolders.Any(f => FileService.HasSizedThumb(f.FullPath, VM.ThumbSize) == false);
            GenMissing.IsEnabled = need;
            GenMissing.Visibility = need ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MatList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // if you use MaterialListItem: set DisplayMemberPath="Name", SelectedValuePath="Id" in XAML
            if (MatList.SelectedItem is MaterialListItem item)
            {
                VM.SelectedMaterial = item;
                AssignBtn.IsEnabled = true;
            }
            else
            {
                VM.SelectedMaterial = null;
                AssignBtn.IsEnabled = false;
            }
        }

        // ===== Folder tile context menu (shows images in leaf folder) =====
        private void FolderTile_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not FolderItem folder)
                return;

            // Only show file list for LEAVES (no subfolders), as requested.
            if (Directory.EnumerateDirectories(folder.FullPath).Any())
                return;

            var cm = new SControls.ContextMenu();

            // “Open in Explorer”
            var miExplorer = new SControls.MenuItem { Header = "Open in explorer" };
            miExplorer.Click += (_, __) => Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = folder.FullPath, UseShellExecute = true });
            cm.Items.Add(miExplorer);
            cm.Items.Add(new SControls.Separator());

            // List images as submenus
            var files = Directory.EnumerateFiles(folder.FullPath)
                                 .Where(FileService.IsImage)
                                 .OrderBy(Path.GetFileName)
                                 .ToList();

            if (files.Count == 0)
            {
                cm.Items.Add(new SControls.MenuItem { Header = "(no image)", IsEnabled = false });
            }
            else
            {
                foreach (var img in files)
                {
                    var fileItem = new SControls.MenuItem { Header = Path.GetFileName(img) };

                    // Set as THUMBNAIL
                    var miThumb = new SControls.MenuItem { Header = "DDefine as thumbnail (128/512/1024)" };
                    miThumb.Click += (_, __) =>
                    {
                        try
                        {
                            ThumbnailService.SetFolderThumbnailFromImage(folder.FullPath, img);
                            folder.TouchThumb();     // refresh tile
                            RecheckGenerateButtonState();
                            ToastService.Show("Thumbnail created.");
                        }
                        catch (Exception ex) { ToastService.Show("Thumbnail: " + ex.Message); }
                    };
                    fileItem.Items.Add(miThumb);
                    fileItem.Items.Add(new SControls.Separator());

                    // Map assignments
                    AddMapAssign(fileItem, "As Diffuse", MapType.Albedo, img);
                    AddMapAssign(fileItem, "As Bump", MapType.Bump, img);
                    AddMapAssign(fileItem, "As Normal", MapType.Bump, img); // still Bump slot
                    AddMapAssign(fileItem, "As Displace", MapType.Bump, img); // still Bump slot
                    AddMapAssign(fileItem, "As Glossiness", MapType.Roughness, img, invert: true);
                    AddMapAssign(fileItem, "As Roughness", MapType.Roughness, img, invert: false);
                    AddMapAssign(fileItem, "As Reflection", MapType.Reflection, img);
                    AddMapAssign(fileItem, "As Refraction", MapType.Refraction, img);
                    AddMapAssign(fileItem, "As Emissive", MapType.Illumination, img);

                    cm.Items.Add(fileItem);
                }
            }

            fe.ContextMenu = cm;
        }

        private void MapSlot_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not MapSlot slot) return;
            var cm = new SControls.ContextMenu();

            // Clear
            var miClear = new SControls.MenuItem { Header = "Clear" };
            miClear.Click += (_, __) => { slot.Assigned = null; slot.Detail = null; };
            cm.Items.Add(miClear);

            // Browse… (open in current grid folder if known)
            var miBrowse = new SControls.MenuItem { Header = "Browse…" };
            miBrowse.Click += (_, __) =>
            {
                var startDir = VM.CurrentGridRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    InitialDirectory = Directory.Exists(startDir) ? startDir : null,
                    Filter = "Images|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.webp"
                };
                if (dlg.ShowDialog() == true)
                {
                    slot.Assigned = new MapFile(dlg.FileName, slot.Type);
                    // If user forces a rough/gloss, flip invert by choice below or keep default:
                }
            };
            cm.Items.Add(miBrowse);
            cm.Items.Add(new Separator());

            // Bump-kind quick switches (only meaningful for Bump)
            if (slot.Type == MapType.Bump)
            {
                void addKind(string title, string detail)
                {
                    var mi = new SControls.MenuItem { Header = title };
                    mi.Click += (_, __) => slot.Detail = detail;
                    cm.Items.Add(mi);
                }
                addKind("Set as Normal", "Normal");
                addKind("Set as Depth", "Depth");
                addKind("Set as Bump", "Bump");
                cm.Items.Add(new Separator());
            }

            // Gloss/Rough toggle (only for Roughness slot)
            if (slot.Type == MapType.Roughness)
            {
                var miR = new SControls.MenuItem { Header = "Set as Roughness" };
                miR.Click += (_, __) => slot.Invert = false;
                var miG = new SControls.MenuItem { Header = "Set as Glossiness" };
                miG.Click += (_, __) => slot.Invert = true;
                cm.Items.Add(miR);
                cm.Items.Add(miG);
            }

            (sender as FrameworkElement)!.ContextMenu = cm;
        }




        // ===== Files overlay at top (opened via right-click on a leaf grid folder) =====

        // A tiny VM for files shown in the overlay
        private sealed class ImageFileVM
        {
            public required string FullPath { get; init; }
            public string FileName => System.IO.Path.GetFileName(FullPath);
        }

        private void GridFoldersPanel_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var fe = e.OriginalSource as FrameworkElement;
            while (fe != null && fe.DataContext is not FolderItem)
                fe = VisualTreeHelper.GetParent(fe) as FrameworkElement;

            if (fe?.DataContext is FolderItem folder)
            {
                // Only for leaf folders (no subfolders)
                if (!Directory.EnumerateDirectories(folder.FullPath).Any())
                {
                    ShowFilesOverlayForFolder(folder.FullPath);
                    e.Handled = true;
                }
            }
        }

        private void ShowFilesOverlayForFolder(string folder)
        {
            var files = Mater2026.Services.FileService
                .EnumerateOriginals(folder)
                .Select(p => new ImageFileVM { FullPath = p })
                .ToList();

            FilesList.ItemsSource = files;
            FilesOverlay.Visibility = files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FilesOverlay_Close_Click(object sender, RoutedEventArgs e)
        {
            FilesOverlay.Visibility = Visibility.Collapsed;
            FilesList.ItemsSource = null;
        }

        // drag source for overlay image tiles
        private void ImageItem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
        }

        private void ImageItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (sender is FrameworkElement fe && fe.Tag is string path && File.Exists(path))
            {
                var data = new System.Windows.DataObject();
                data.SetData("Mater2026.FilePath", path);

                var files = new System.Collections.Specialized.StringCollection { path };
                data.SetFileDropList(files);

                DragDrop.DoDragDrop(fe, data, System.Windows.DragDropEffects.Copy);
            }
        }

        // Also fully-qualify DragEventArgs to avoid WinForms clash:
        private void MapSlot_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            bool ok = e.Data.GetDataPresent("Mater2026.FilePath") || e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop);
            e.Effects = ok ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private void MapSlot_Drop(object sender, System.Windows.DragEventArgs e)
        {
            string? path = null;

            if (e.Data.GetDataPresent("Mater2026.FilePath"))
                path = e.Data.GetData("Mater2026.FilePath") as string;
            else if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                path = ((string[])e.Data.GetData(System.Windows.DataFormats.FileDrop))?.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            if (sender is FrameworkElement { DataContext: Mater2026.Models.MapSlot slot })
            {
                slot.Assigned = new Mater2026.Models.MapFile(path, slot.Type);
                MapTypesList.Items.Refresh();
            }
        }

        // ===== File item context menu (overlay) =====
        private void ImageItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not string path || !File.Exists(path))
                return;

            var cm = new SControls.ContextMenu();

            // Set as thumbnail
            {
                var mi = new SControls.MenuItem { Header = "Définir comme miniature (128/512/1024)" };
                mi.Click += (_, __) => SetAsThumbnail(path);
                cm.Items.Add(mi);
                cm.Items.Add(new Separator());
            }

            // Map type helpers
            void add(string header, MapType type)
            {
                var mi = new SControls.MenuItem { Header = header, Tag = type };
                mi.Click += (_, __) => SetAsMap(path, type);
                cm.Items.Add(mi);
            }

            add("Définir comme Diffuse/Albedo", MapType.Albedo);
            add("Définir comme Roughness", MapType.Roughness);
            add("Définir comme Glossiness", MapType.Roughness /* invert handled later if needed */);
            add("Définir comme Reflection", MapType.Reflection);
            add("Définir comme Bump/Normal", MapType.Bump);
            add("Définir comme Displacement", MapType.Bump);
            add("Définir comme Opacity", MapType.Refraction);
            add("Définir comme Emissive", MapType.Illumination);

            fe.ContextMenu = cm;
        }

        private void SetAsThumbnail(string srcPath)
        {
            try
            {
                var folder = Path.GetDirectoryName(srcPath)!;
                var baseName = Path.GetFileName(folder);
                var ext = Path.GetExtension(srcPath);

                // Copy the chosen file as the "key" image (foldername.ext)
                var baseFile = Path.Combine(folder, baseName + ext);
                File.Copy(srcPath, baseFile, overwrite: true);

                // Generate sizes (overwriting)
                ThumbnailService.GenerateThumb(folder, 128, overwrite: true);
                ThumbnailService.GenerateThumb(folder, 512, overwrite: true);
                ThumbnailService.GenerateThumb(folder, 1024, overwrite: true);

                // Refresh grid thumbs & button state
                RefreshThumbsAndButton();
                ToastService.Show("Miniatures mises à jour.");
            }
            catch (Exception ex)
            {
                ToastService.Show("Échec miniature : " + ex.Message);
            }
        }

        private void SetAsMap(string path, MapType type)
        {
            try
            {
                // Find or create the slot
                var slot = VM.MapTypes.FirstOrDefault(s => s.Type == type);
                if (slot == null)
                {
                    slot = new MapSlot(type);
                    VM.MapTypes.Add(slot);
                }

                slot.Assigned = new MapFile(path, type);
                MapTypesList.Items.Refresh();
                ToastService.Show($"Affecté au slot {type}.");
            }
            catch (Exception ex)
            {
                ToastService.Show("Affectation map : " + ex.Message);
            }
        }

        private void AddMapAssign(SControls.MenuItem parent, string header, MapType type, string imgPath, bool invert = false)
        {
            var mi = new SControls.MenuItem { Header = header };
            mi.Click += (_, __) =>
            {
                // find slot
                var slot = VM.MapTypes.FirstOrDefault(s => s.Type == type) ?? new MapSlot(type);
                if (!VM.MapTypes.Contains(slot)) VM.MapTypes.Add(slot);

                slot.Assigned = new MapFile(imgPath, type);
                if (type == MapType.Roughness) slot.Invert = invert;

                MapTypesList.Items.Refresh();
                ToastService.Show($"Affecté: {header}");
            };
            parent.Items.Add(mi);
        }

        // ===== Commands wiring =====
        private void AssignBtn_Click(object sender, RoutedEventArgs e) => VM.AssignCommand.Execute(null);
        private void PipetteBtn_Click(object sender, RoutedEventArgs e) => VM.PipetteCommand.Execute(null);

        private void MatNameBox_TextChanged(object sender, TextChangedEventArgs e)
            => VM.Params.MaterialName = (sender as SControls.TextBox)?.Text ?? "";

        private async void CreateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(WidthBox.Text, out var w)) VM.Params.WidthCm = w;
            if (double.TryParse(HeightBox.Text, out var h)) VM.Params.HeightCm = h;
            if (double.TryParse(RotBox.Text, out var r)) VM.Params.RotationDeg = r;
            if (int.TryParse(TilesXBox.Text, out var tx)) VM.Params.TilesX = tx;
            if (int.TryParse(TilesYBox.Text, out var ty)) VM.Params.TilesY = ty;

            await VM.CreateMaterialAndAppearanceFromParams();
        }

        private async void ReplaceBtn_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(WidthBox.Text, out var w)) VM.Params.WidthCm = w;
            if (double.TryParse(HeightBox.Text, out var h)) VM.Params.HeightCm = h;
            if (double.TryParse(RotBox.Text, out var r)) VM.Params.RotationDeg = r;
            if (int.TryParse(TilesXBox.Text, out var tx)) VM.Params.TilesX = tx;
            if (int.TryParse(TilesYBox.Text, out var ty)) VM.Params.TilesY = ty;

            if (VM.SelectedMaterial == null) await VM.CreateMaterialAndAppearanceFromParams();
            else await VM.ReplaceSelectedMaterialAppearanceFromParams();
        }

        // ===== Standard params after detection (rotation=0°, name=appearance name) =====
        private void SetStandardParamsAfterDetection(string folderPath)
        {
            VM.Params.FolderPath = folderPath;
            VM.Params.MaterialName = Path.GetFileName(folderPath); // appearance name
            VM.Params.WidthCm = 300.0;  // 3m
            VM.Params.HeightCm = 300.0; // 3m
            VM.Params.RotationDeg = 0.0; // reset rotation
            VM.Params.TilesX = 1;
            VM.Params.TilesY = 1;
            VM.Params.Tint = (255, 255, 255); // white
        }

        private void TintBtn_Click(object sender, RoutedEventArgs e)
        {
            using var cd = new WinForms.ColorDialog();
            if (cd.ShowDialog() == WinForms.DialogResult.OK)
            {
                VM.Params.Tint = (cd.Color.R, cd.Color.G, cd.Color.B);
                TintBtn.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(cd.Color.R, cd.Color.G, cd.Color.B));
            }
        }
    }
}
