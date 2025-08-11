using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using MaterRevitAddin.ViewModels;
using System.Linq;

namespace MaterRevitAddin.ExternalEvents
{
    public class PickMaterialHandler : IExternalEventHandler
    {
        public static ExternalEvent Event { get; } = ExternalEvent.Create(new PickMaterialHandler());
        public static PickMaterialHandler Instance { get; } = new PickMaterialHandler();
        public MaterViewModel? VM { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc.Document;
            if (VM == null) return;

            try
            {
                var r = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Choisir une face (Pipette)");
                var el = doc.GetElement(r);
                var face = el.GetGeometryObjectFromReference(r) as Face;

                Material? mat = null;
                if (face != null) mat = doc.GetPaintedMaterial(el.Id, face);
                if (mat == null)
                {
                    var mids = el.GetMaterialIds(true);
                    mat = mids.Select(id => doc.GetElement(id) as Material).FirstOrDefault();
                }
                if (mat == null) { TaskDialog.Show("Pipette", "Aucun matériau trouvé."); return; }

                var appe = doc.GetElement(mat.AppearanceAssetId) as AppearanceAssetElement;
                if (appe == null) { TaskDialog.Show("Pipette", "Aucune apparence liée."); return; }

                var asset = appe.GetRenderingAsset();
                VM.MaterialName = mat.Name;
                VM.AppearanceName = appe.Name;
                VM.Description = appe.Description;

                var diff = asset.FindByName("generic_diffuse");
                if (diff != null && diff.NumberOfConnectedProperties > 0)
                {
                    var ub = diff.GetConnectedProperty(0) as Asset;
                    if (ub?.FindByName("unifiedbitmap_realworldsizex") is AssetPropertyDouble sx) VM.RealWorldSizeX = sx.Value * 0.0254;
                    if (ub?.FindByName("unifiedbitmap_realworldsizey") is AssetPropertyDouble sy) VM.RealWorldSizeY = sy.Value * 0.0254;
                    if (ub?.FindByName("unifiedbitmap_rotation") is AssetPropertyDouble rot) VM.RotationAngle = rot.Value;
                }

                var path = GetBitmap(asset, "generic_diffuse");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var folder = System.IO.Path.GetDirectoryName(path);
                    if (System.IO.Directory.Exists(folder))
                        VM.GetType().GetMethod("LoadGallery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                          ?.Invoke(VM, new object[] { folder! });
                }
            }
            catch { /* ESC or error */ }
        }

        static string? GetBitmap(Asset a, string prop)
        {
            var p = a.FindByName(prop);
            if (p == null || p.NumberOfConnectedProperties == 0) return null;
            var sub = p.GetConnectedProperty(0) as Asset;
            var s = sub?.FindByName("unifiedbitmap_Bitmap") as AssetPropertyString;
            return s?.Value;
        }

        public string GetName() => "Pipette lecture-seule";
    }
}
