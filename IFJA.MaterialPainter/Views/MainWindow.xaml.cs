using System.Linq;
using System.Windows;
using MaterRevitAddin.ViewModels;

namespace MaterRevitAddin.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() { InitializeComponent(); }
        private MaterViewModel VM => (MaterViewModel)DataContext;

        private System.Windows.Point _dragStartPoint;

        private void BtnPipette_Click(object sender, RoutedEventArgs e) => VM.PickMaterialFromModelCommand.Execute(null);

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is MaterRevitAddin.Models.DirectoryItem di)
            {
                VM.GetType().GetMethod("LoadGallery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                  ?.Invoke(VM, new object[] { di.FullPath });
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Back)
            {
                try
                {
                    var cur = VM.CurrentFolderPath;
                    var parent = System.IO.Directory.GetParent(cur);
                    if (parent != null)
                    {
                        VM.GetType().GetMethod("LoadGallery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                          ?.Invoke(VM, new object[] { parent.FullName });
                    }
                } catch { }
            }
        }

        // Gallery drag & drop
        private void Gallery_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void Gallery_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
            var pos = e.GetPosition(null);
            if (System.Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                System.Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (GalleryCtrl.SelectedItems == null || GalleryCtrl.SelectedItems.Count == 0) return;
            var list = new System.Collections.Generic.List<string>();
            foreach (var it in GalleryCtrl.SelectedItems)
                if (it is MaterRevitAddin.Models.MapItem mi && !string.IsNullOrEmpty(mi.FullPath))
                    list.Add(mi.FullPath);
            if (list.Count == 0) return;

            var data = new System.Windows.DataObject();
            data.SetData("FileDropFromGallery", list.ToArray());
            data.SetData(System.Windows.DataFormats.FileDrop, list.ToArray());
            System.Windows.DragDrop.DoDragDrop(GalleryCtrl, data, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Link);
        }

        private void Gallery_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) || e.Data.GetDataPresent("FileDropFromGallery"))
                e.Effects = System.Windows.DragDropEffects.Copy;
            else
                e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private void Gallery_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                MaterRevitAddin.Utils.FolderService.IngestPathsToFolder(files, VM.CurrentFolderPath, move:false, overwrite:true);
                VM.GetType().GetMethod("LoadGallery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                  ?.Invoke(VM, new object[] { VM.CurrentFolderPath });
            }
        }

        // Slots drop
        private void Slots_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) || e.Data.GetDataPresent("FileDropFromGallery"))
                e.Effects = System.Windows.DragDropEffects.Link;
            else
                e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private void Slots_Drop(object sender, DragEventArgs e)
        {
            string[] files = null;
            if (e.Data.GetDataPresent("FileDropFromGallery"))
                files = (string[])e.Data.GetData("FileDropFromGallery");
            else if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            var img = files.FirstOrDefault(f => MaterRevitAddin.Utils.FolderService.IsImage(f));
            if (string.IsNullOrEmpty(img)) return;

            var det = MaterRevitAddin.Utils.MapFileUtils.Detect(img);
            var mi = VM.MapSlots.FirstOrDefault(s => s.SlotType == det.slot);
            if (mi != null)
            {
                if (!mi.Alternatives.Contains(img)) mi.Alternives.Add(img); // <-- corrigé ci-dessous
                mi.SelectedAlternative = img;
            }
        }

        // Tree drop (copy by default; Shift = move)
        private void Tree_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) || e.Data.GetDataPresent("FileDropFromGallery"))
                e.Effects = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0
                    ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.Copy;
            else
                e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private System.Windows.Controls.TreeViewItem? GetTreeViewItemFromPoint(System.Windows.Controls.TreeView tree, System.Windows.Point point)
        {
            var element = tree.InputHitTest(point) as System.Windows.DependencyObject;
            while (element != null && element is not System.Windows.Controls.TreeViewItem)
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            return element as System.Windows.Controls.TreeViewItem;
        }

        private void Tree_Drop(object sender, DragEventArgs e)
        {
            var tree = FolderTree;
            var tvi = GetTreeViewItemFromPoint(tree, e.GetPosition(tree));
            if (tvi == null) return;
            if (tvi.DataContext is not MaterRevitAddin.Models.DirectoryItem di) return;
            var targetFolder = di.FullPath;
            if (string.IsNullOrEmpty(targetFolder)) return;

            string[] files = System.Array.Empty<string>();
            if (e.Data.GetDataPresent("FileDropFromGallery"))
                files = (string[])e.Data.GetData("FileDropFromGallery");
            else if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);

            if (files == null || files.Length == 0) return;

            bool move = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
            MaterRevitAddin.Utils.FolderService.IngestPathsToFolder(files, targetFolder, move, overwrite:true);

            if (string.Equals(targetFolder, VM.CurrentFolderPath, System.StringComparison.OrdinalIgnoreCase))
            {
                VM.GetType().GetMethod("LoadGallery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                  ?.Invoke(VM, new object[] { VM.CurrentFolderPath });
            }
        }
    }
}
