
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
namespace IFJA.MaterialPainter
{
  public class Command : IExternalCommand
  {
    public Result Execute(ExternalCommandData d, ref string m, ElementSet e)
    {
      var w = new Views.MainWindow();
      w.Show();
      return Result.Succeeded;
    }
  }
}
