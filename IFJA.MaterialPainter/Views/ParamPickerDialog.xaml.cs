using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;

namespace MaterRevitAddin.Views
{
    public partial class ParamPickerDialog : Window
    {
        public Parameter? Selected { get; private set; }
        public ParamPickerDialog(IEnumerable<Parameter> inst, IEnumerable<Parameter> type)
        {
            InitializeComponent();
            ListInstance.ItemsSource = inst;
            ListType.ItemsSource = type;
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Selected = (Parameter?) (ListInstance.SelectedItem ?? ListType.SelectedItem);
            if (Selected == null) { MessageBox.Show("Sélectionnez un paramètre."); return; }
            DialogResult = true;
        }
        public static Parameter? Show(Window owner, IEnumerable<Parameter> inst, IEnumerable<Parameter> type)
        {
            var dlg = new ParamPickerDialog(inst, type) { Owner = owner };
            return dlg.ShowDialog() == true ? dlg.Selected : null;
        }
    }
}
