using Autodesk.Revit.UI;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;

namespace Mater2026
{
    public partial class MaterWindow : Window
    {
        private readonly MaterViewModel _vm;

        public MaterWindow(UIDocument uidoc)
        {
            InitializeComponent();
            _vm = new MaterViewModel(uidoc);
            DataContext = _vm;
            // Chemin racine à ajuster selon ton arborescence de textures
            string baseDir = @"Z:\Textures";
            _ = _vm.InitializeTreeRootAsync(baseDir);
        }

        // =============================================
        // 🔹 Synchronisation du TreeView / Grid / Breadcrumb
        // =============================================

        // Sélectionne et déploie récursivement le TreeView jusqu’à un dossier cible
        // =======================================================
        // 🔹 Sélection / Expansion dans le TreeView
        // =======================================================
        private static bool ExpandAndSelectNode(ItemsControl parent, string fullPath)
        {
            foreach (var item in parent.Items)
            {
                if (item is not FolderNode node)
                    continue;

                // Récupère le conteneur TreeViewItem
                var container = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                if (container == null)
                {
                    // Force la génération des conteneurs si non encore rendus
                    parent.UpdateLayout();
                    container = parent.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                }

                if (container == null)
                    continue;

                // 🔹 Correspondance exacte du chemin → sélection
                if (node.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    container.IsSelected = true;
                    container.BringIntoView();
                    container.IsExpanded = true;
                    return true;
                }

                // 🔹 Recherche récursive (descend dans les sous-dossiers)
                if (node.Children.Any() && ExpandAndSelectNode(container, fullPath))
                {
                    container.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }
        private void FolderTreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item && item.DataContext is FolderNode node)
            {
                node.EnsureChildrenLoaded();
            }
        }



        // =======================================================
        // 🔹 Événements UI : TreeView, Breadcrumb, Miniatures
        // =======================================================

        // Sélection d’un dossier dans le TreeView
        private async void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FolderNode node)
                await _vm.OnFolderSelectedAsync(node.FullPath);
        }

        // Clic sur le fil d’Ariane
        private async void Breadcrumb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                // Navigation via ViewModel
                await _vm.NavigateToFolderAsync(path);

                // Synchronise le TreeView visuellement
                ExpandAndSelectNode(FolderTree, path);
            }
        }

        // Clic sur une miniature (ouvre le dossier correspondant)
        private async void Thumb_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ThumbItem ti)
            {
                if (Directory.EnumerateDirectories(ti.FullPath).Any())
                {
                    // If thumbnail is a folder, descend into it
                    ExpandAndSelectNode(FolderTree, ti.FullPath);
                }
                else
                {
                    // If no subfolders → treat as a material folder
                    await _vm.OnFolderSelectedAsync(ti.FullPath);
                }
            }
        }



        // =============================================
        // 🔹 Drag & Drop des textures
        // =============================================
        private void MapSlot_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = files.All(FileService.IsImage) ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void MapSlot_Drop(object sender, DragEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MapSlot slot &&
                e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string? file = files.FirstOrDefault(FileService.IsImage);
                if (file != null)
                    slot.Assigned = new MapFile(file, slot.Type);
            }
        }

        // =============================================
        // 🔹 Sélection de fichier et teinte
        // =============================================
        private void MapSlot_Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MapSlot slot)
            {
                string? baseDir = _vm.Ui.FolderPath;
                string? path = FileBrowserService.BrowseForImage(baseDir);
                if (!string.IsNullOrEmpty(path))
                {
                    slot.Assigned = new MapFile(path, slot.Type);
                }
            }
        }


        private void Tint_Click(object sender, RoutedEventArgs e)
        {
            var color = ColorDialogService.PickColor();
            if (color != null)
                _vm.Ui.Tint = color;
        }
    }
}
