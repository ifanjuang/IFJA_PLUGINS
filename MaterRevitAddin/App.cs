using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace Mater2026
{
    public class App : IExternalApplication
    {
        public static ExternalEvent? PickEvent { get; private set; }
        public static PickMaterialHandler? PickHandler { get; private set; }

        public static ExternalEvent? ApplyEvent { get; private set; }
        public static ApplyMaterialHandler? ApplyHandler { get; private set; }

        public static ExternalEvent? FolderEvent { get; private set; }
        public static FolderStorageHandler? FolderHandler { get; private set; }



        public Result OnStartup(UIControlledApplication a)
        {
            try
            {
                // --- Utiliser le ruban "Compléments" natif de Revit ---
                string tabName = "Add-Ins"; // Revit détecte automatiquement "Compléments" si l’interface est en français

                // Crée ou récupère le panneau dans l’onglet Compléments
                RibbonPanel panel;
                try
                {
                    var panels = a.GetRibbonPanels(tabName);
                    panel = panels.FirstOrDefault(p => p.Name == "Mater2026")
                            ?? a.CreateRibbonPanel(tabName, "Mater2026");
                }
                catch
                {
                    // Si l’onglet n’existe pas encore (rare), crée un panneau temporaire
                    panel = a.CreateRibbonPanel("Mater2026");
                }

                string path = Assembly.GetExecutingAssembly().Location;

                var button = new PushButtonData(
                    "Mater2026Btn",
                    "Mater 2026",
                    path,
                    "Mater2026.ShowWindowCmd")
                {
                    ToolTip = "Open Mater 2026 material manager",
                    LargeImage = LoadPngImageSource("icon32.png"),
                    Image = LoadPngImageSource("icon16.png")
                };

                panel.AddItem(button);

                PickHandler = new PickMaterialHandler();
                PickEvent = ExternalEvent.Create(PickHandler);

                ApplyHandler = new ApplyMaterialHandler();
                ApplyEvent = ExternalEvent.Create(ApplyHandler);

                FolderHandler = new FolderStorageHandler();
                FolderEvent = ExternalEvent.Create(FolderHandler);

            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater2026", "Startup failed: " + ex.Message);
                return Result.Failed;
            }

            return Result.Succeeded;
        }


        public Result OnShutdown(UIControlledApplication a) => Result.Succeeded;

        // Charge une icône PNG embarquée en ressource
        private static BitmapImage? LoadPngImageSource(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                // Correction : s'assurer que le namespace complet est utilisé
                var fullName = assembly.GetManifestResourceNames()
                                       .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
                if (fullName == null)
                {
                    TaskDialog.Show("Mater2026", $"Resource not found: {resourceName}");
                    return null;
                }

                using Stream? stream = assembly.GetManifestResourceStream(fullName);
                if (stream == null) return null;

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater2026", $"Error loading {resourceName}: {ex.Message}");
                return null;
            }
        }

    }

    // Commande pour afficher la fenêtre
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowWindowCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet elems)
        {
            try
            {
                var uiapp = data.Application;
                var win = SingletonWindow.Get(uiapp);

                if (win.WindowState == WindowState.Minimized)
                    win.WindowState = WindowState.Normal;

                win.Show();
                win.Activate();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater2026", "Erreur : " + ex.Message);
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }


    internal static class SingletonWindow
    {
        private static MaterWindow? _win;

        public static MaterWindow Get(UIApplication uiapp)
        {
            if (_win == null)
            {
                if (uiapp.ActiveUIDocument == null)
                    throw new InvalidOperationException("No active document in Revit.");

                _win = new MaterWindow(uiapp.ActiveUIDocument);

                // Attacher la fenêtre à Revit
                var helper = new WindowInteropHelper(_win)
                {
                    Owner = uiapp.MainWindowHandle
                };

                // Libère la référence quand la fenêtre est fermée
                _win.Closed += (s, e) => _win = null;
            }
            return _win;
        }
    }
}
