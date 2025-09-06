using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Mater2026.Services;
using Mater2026.ViewModels;

namespace Mater2026.Handlers
{
    public class ReplaceAppearanceHandler : IExternalEventHandler
    {
        public MaterViewModel? VM { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument; if (uidoc == null) return;
            var doc = uidoc.Document; if (doc == null) return;
            var sel = VM?.SelectedMaterial; if (sel == null) return;

            try
            {
                var mat = doc.GetElement(sel.Id) as Material;
                if (mat == null) return;

                // Assurer une apparence "générique-like" dupliquée
                RevitMaterialService.EnsureGenericAppearance(doc, mat);

                // Construire les maps courantes (depuis la colonne de droite / dossier)
                var maps = VM!.MapTypes
                    .Where(s => s.Assigned != null)
                    .ToDictionary(
                        s => s.Type,
                        s => (path: (string?)s.Assigned!.FullPath, invert: s.Invert, detail: s.Detail)
                    );

                // Appliquer au matériau sélectionné (nouvelle apparence éditée)
                RevitMaterialService.ApplyUiToMaterial(
                    doc, mat, maps,
                    VM.Params.WidthCm, VM.Params.HeightCm,
                    VM.Params.RotationDeg, VM.Params.Tint);

                VM.LogInfo($"Apparence remplacée pour « {mat.Name} ».");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Mater2026 – Remplacer l’apparence", ex.Message);
                VM?.LogError(ex);
            }
        }

        public string GetName() => "Mater2026.ReplaceAppearance";
    }
}
