using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using MaterRevitAddin.ViewModels;
using MaterRevitAddin.Utils;
using System;

namespace MaterRevitAddin.ExternalEvents
{
    public class CreatePatternFromDivisionsHandler : IExternalEventHandler
    {
        public static ExternalEvent Event { get; } = ExternalEvent.Create(new CreatePatternFromDivisionsHandler());
        public static CreatePatternFromDivisionsHandler Instance { get; } = new CreatePatternFromDivisionsHandler();
        public MaterViewModel? VM { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            if (VM == null) return;
            using var t = new Transaction(doc, "Créer/MAJ motif");
            t.Start();

            double x_m = VM.RealWorldSizeX;
            double y_m = VM.RealWorldSizeY;
            int dx = Math.Max(0, VM.DivX);
            int dy = Math.Max(0, VM.DivY);

            double? sx_m = dx > 0 ? x_m / dx : (double?)null;
            double? sy_m = dy > 0 ? y_m / dy : (double?)null;

            if (sx_m == null && sy_m == null)
            {
                TaskDialog.Show("Motif", "DivX=0 et DivY=0 : aucun motif créé.");
                t.RollBack();
                return;
            }

            string name = $"tile_{(int)Math.Round((sx_m ?? 0)*1000)}_{(int)Math.Round((sy_m ?? 0)*1000)}";

            FillPattern fp = new FillPattern(name, FillPatternTarget.Model, FillPatternHostOrientation.ToView);
            if (sx_m.HasValue) fp.AddGrid(90.0, AssetUtils.MetersToFeet(sx_m.Value), 0, 0);
            if (sy_m.HasValue) fp.AddGrid(0.0, AssetUtils.MetersToFeet(sy_m.Value), 0, 0);

            FillPatternElement? existing = null;
            foreach (var fpeId in FillPatternElement.GetFillPatternElementIds(doc))
            {
                var fpe = doc.GetElement(fpeId) as FillPatternElement;
                if (fpe != null && fpe.GetFillPattern().Name == name && fpe.GetFillPattern().IsModel) { existing = fpe; break; }
            }

            if (existing == null) FillPatternElement.Create(doc, fp);
            else existing.SetFillPattern(fp);

            t.Commit();
        }

        public string GetName() => "Create Pattern From Divisions";
    }
}
