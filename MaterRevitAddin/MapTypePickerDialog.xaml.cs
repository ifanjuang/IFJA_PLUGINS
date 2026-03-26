using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Mater2026
{
    public partial class MapTypePickerDialog : Window
    {
        /// <summary>
        /// The selected MapType, or null if user chose "Preview".
        /// </summary>
        public MapType? SelectedMapType { get; private set; }

        /// <summary>
        /// True if user confirmed, false if cancelled.
        /// </summary>
        public bool Confirmed { get; private set; }

        public MapTypePickerDialog(string filePath)
        {
            InitializeComponent();
            FileNameText.Text = Path.GetFileName(filePath);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;

            if (TypeList.SelectedItem is ListBoxItem item && item.Tag is string tag)
            {
                SelectedMapType = tag switch
                {
                    "Albedo" => MapType.Albedo,
                    "Bump" => MapType.Bump,
                    "Roughness" => MapType.Roughness,
                    "Reflection" => MapType.Reflection,
                    "Refraction" => MapType.Refraction,
                    "Illumination" => MapType.Illumination,
                    _ => null // "Preview"
                };
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Shows the dialog and returns the chosen MapType, or null for "Preview".
        /// Returns null with Confirmed=false if the user cancels entirely.
        /// </summary>
        public static MapType? Ask(string filePath, Window? owner = null)
        {
            var dlg = new MapTypePickerDialog(filePath);
            if (owner != null)
                dlg.Owner = owner;

            if (dlg.ShowDialog() == true && dlg.Confirmed)
                return dlg.SelectedMapType;

            return null;
        }
    }
}
