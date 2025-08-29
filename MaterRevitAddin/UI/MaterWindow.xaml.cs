using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using Mater2026.Services;
using Mater2026.ViewModels;
// Aliases to avoid ambiguity with WinForms and Revit
using WinForms = System.Windows.Forms;
using SControls = System.Windows.Controls;
using Mater2026.Models;

using WpfApp = System.Windows.Application;
using SMessageBox = System.Windows.MessageBox;
using Autodesk.Revit.UI.Events;

namespace Mater2026.UI
{
    public partial class MaterWindow : Window
    {
        public MaterViewModel VM { get; }
        private FileSystemWatcher? _watcher;
        private static readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly CancellationTokenSource _thumbCts = cancellationTokenSource;

        public MaterWindow(UIApplication uiapp)
        {
            InitializeComponent();
            VM = new MaterViewModel(uiapp);
            DataContext = new { VM };

            // Bind lists
            MatList.ItemsSource = VM.ProjectMaterials;
            GridFoldersPanel.ItemsSource = VM.GridFolders;
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

            // Tree context menu: open in Explorer (use WPF menu, not Revit.UI)
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

            // Restore last root + default thumb size (256)
            Loaded += async (_, __) =>
            {
                EnsureThumbDropDefaults();

                var last = SettingsService.LoadLastRoot();
                if (!string.IsNullOrWhiteSpace(last) && Directory.Exists(last))
                {
                    RootBox.Text = last;
                    await BuildTreeAsync(last);
                    await SelectTreeNodeAsync(last);
                }
            };
        }

        private void ToggleGallery_Click(object sender, RoutedEventArgs e)
        {
            // Simple toggle; if you later add different views/templates, switch them here
            VM.IsGallery = !VM.IsGallery;
        }


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

        // ===== Tree (lazy/async), show only folders that themselves have subfolders =====
        private async Task BuildTreeAsync(string rootPath)
        {
            Tree.Items.Clear();

            var rootItem = new SControls.TreeViewItem
            {
                Header = Path.GetFileName(rootPath),
                Tag = rootPath
            };
            AttachContextMenu(rootItem);
            Tree.Items.Add(rootItem);

            AddLazyStub(rootItem);
            rootItem.Expanded += async (s, e) =>
                await PopulateChildrenAsync((SControls.TreeViewItem)s!, (string)((SControls.TreeViewItem)s!).Tag);

            rootItem.IsExpanded = true;
            await PopulateChildrenAsync(rootItem, rootPath); // first level
        }

        private static bool HasSubdirectory(string dir)
        {
            try { return Directory.EnumerateDirectories(dir).Any(); }
            catch { return false; }
        }

        private static void AddLazyStub(SControls.TreeViewItem item)
        {
            item.Items.Clear();
            item.Items.Add(new SControls.TreeViewItem { Header = "…" }); // stub to show expander
        }

        private static async Task PopulateChildrenAsync(SControls.TreeViewItem parent, string path)
        {
            parent.Items.Clear();
            string[] subs;
            try { subs = await Task.Run(() => Directory.EnumerateDirectories(path).ToArray()); }
            catch { return; }

            foreach (var sub in subs)
            {
                if (!HasSubdirectory(sub)) continue; // only keep nodes that themselves have subfolders

                var child = new SControls.TreeViewItem
                {
                    Header = Path.GetFileName(sub),
                    Tag = sub
                };
                AttachContextMenu(child);
                AddLazyStub(child);
                child.Expanded += async (s, e) =>
                    await PopulateChildrenAsync((SControls.TreeViewItem)s!, (string)((SControls.TreeViewItem)s!).Tag);

                parent.Items.Add(child);
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
            // Populate grid with direct children (VM handles leaf/non-leaf thumbs)
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
        // Call this in Loaded (already done in your constructor)
        private void EnsureThumbDropDefaults()
        {
            ThumbSizeDrop.Items.Clear();
            ThumbSizeDrop.Items.Add(128);
            ThumbSizeDrop.Items.Add(512);
            ThumbSizeDrop.Items.Add(1024);

            ThumbSizeDrop.SelectedItem = 128; // default smallest
            VM.ThumbSize = 128;
        }

        private void ThumbSizeDrop_SelectionChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ThumbSizeDrop.SelectedItem is int size)
            {
                VM.ThumbSize = size;
                foreach (var f in VM.GridFolders)
                {
                    f.ThumbSize = size;
                    f.TouchThumb();
                }
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


        // ===== Materials list & commands =====
        private void MatList_SelectionChanged(object sender, SControls.SelectionChangedEventArgs e)
        {
            if (MatList.SelectedItem is ValueTuple<Autodesk.Revit.DB.ElementId, string> tuple)
            {
                VM.SelectedMaterial = tuple;
                AssignBtn.IsEnabled = true;
            }
            else
            {
                VM.SelectedMaterial = null;
                AssignBtn.IsEnabled = false;
            }
        }

        private void AssignBtn_Click(object sender, RoutedEventArgs e) => VM.AssignCommand.Execute(null);
        private void PipetteBtn_Click(object sender, RoutedEventArgs e) => VM.PipetteCommand.Execute(null);

        private void MatNameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => VM.Params.MaterialName = (sender as System.Windows.Controls.TextBox)?.Text ?? "";


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
            VM.Params.HeightCm = 300.0;  // 3m
            VM.Params.RotationDeg = 0.0;    // reset rotation
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
