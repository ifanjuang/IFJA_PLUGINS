using System.Windows;

namespace MaterRevitAddin.Views
{
    public partial class InputDialog : Window
    {
        public string ResultText { get; private set; } = "";
        public InputDialog(string prompt, string initial = "")
        {
            InitializeComponent();
            Prompt.Text = prompt;
            Box.Text = initial ?? "";
            Loaded += (_, __) => { Box.Focus(); Box.SelectAll(); };
        }
        private void Ok_Click(object sender, RoutedEventArgs e) { ResultText = Box.Text; DialogResult = true; }
        public static string? Show(Window owner, string prompt, string initial = "")
        {
            var dlg = new InputDialog(prompt, initial) { Owner = owner };
            return dlg.ShowDialog() == true ? dlg.ResultText : null;
        }
    }
}