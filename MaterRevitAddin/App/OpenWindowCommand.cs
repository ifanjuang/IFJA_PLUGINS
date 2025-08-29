// App/OpenWindowCommand.cs
using System;
using System.Diagnostics;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Mater2026.App
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class OpenWindowCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;

            if (AddinApp.Window == null)
            {
                AddinApp.Window = new UI.MaterWindow(uiapp);
                AddinApp.Window.Closed += (s, e) => AddinApp.Window = null;

                // Set Revit as owner so the window stays on top of Revit
                try
                {
                    var hwnd = Process.GetCurrentProcess().MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        var interop = new WindowInteropHelper(AddinApp.Window)
                        {
                            Owner = hwnd
                        };
                    }
                }
                catch { /* ignore */ }

                AddinApp.Window.Show();
            }
            else
            {
                if (!AddinApp.Window.IsVisible) AddinApp.Window.Show();
                AddinApp.Window.Activate();
            }

            return Result.Succeeded;
        }
    }
}
