using System.Linq;
using Autodesk.Revit.DB;

namespace Mater2026.Services
{
    public static class MaterialPickService
    {
        public static ElementId? SampleFromReference(Document doc, Reference r)
        {
            var elem = doc.GetElement(r);
            if (elem == null) return null;

            if (elem.GetGeometryObjectFromReference(r) is Face face)
            {
                if (doc.IsPainted(elem.Id, face))
                    return doc.GetPaintedMaterial(elem.Id, face);

                if (face.MaterialElementId != ElementId.InvalidElementId)
                    return face.MaterialElementId;
            }

            var mats = elem.GetMaterialIds(false);
            if (mats != null && mats.Count > 0)
                return mats.First();

            return null;
        }
    }
}
