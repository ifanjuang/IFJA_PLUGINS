using Autodesk.Revit.UI;
using MaterRevitAddin.ViewModels;
using MaterRevitAddin.Views;

namespace MaterRevitAddin.Commands
{
    public class ShowUICommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            var uiapp = commandData.Application;
            var vm = new MaterViewModel(uiapp);
            var win = new MainWindow { DataContext = vm };
            win.Show();
            return Result.Succeeded;
        }
    }
}
