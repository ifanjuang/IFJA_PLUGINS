// App/AddinApp.cs
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace Mater2026.App
{
    public class AddinApp : IExternalApplication
    {
        public static UIControlledApplication? UiApp { get; private set; }
        public static UI.MaterWindow? Window { get; internal set; }

        public Result OnStartup(UIControlledApplication application)
        {
            UiApp = application;

            var panel = application.CreateRibbonPanel("Mater 2026");

            var btnData = new PushButtonData(
                "MaterOpen",
                "Mater 2026",
                Assembly.GetExecutingAssembly().Location,
                "Mater2026.App.OpenWindowCommand"
            );

            if (panel.AddItem(btnData) is PushButton btn)
            {
                btn.ToolTip = "Ouvrir Mater (fenêtre modeless)";

                // Optional ribbon icons
                try
                {
                    var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                    var icon32 = Path.Combine(baseDir, "Resources", "mater_32.png");
                    var icon16 = Path.Combine(baseDir, "Resources", "mater_16.png");

                    if (File.Exists(icon32))
                        btn.LargeImage = new BitmapImage(new Uri(icon32, UriKind.Absolute));
                    if (File.Exists(icon16))
                        btn.Image = new BitmapImage(new Uri(icon16, UriKind.Absolute));
                }
                catch { /* ignore icon issues */ }
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            if (Window != null)
            {
                try { Window.Close(); } catch { }
                Window = null;
            }
            return Result.Succeeded;
        }
    }
}
