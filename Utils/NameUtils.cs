using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace MaterRevitAddin.Utils
{
    public static class NameUtils
    {
        public static string GetUniqueMaterialName(Document doc, string baseName)
        {
            string name = baseName;
            int i = 1;
            while (new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                name = $"{baseName}_{i++}";
            return name;
        }

        public static string GetUniqueAppearanceName(Document doc, string baseName)
        {
            string name = baseName;
            int i = 1;
            while (new FilteredElementCollector(doc).OfClass(typeof(AppearanceAssetElement)).Cast<AppearanceAssetElement>().Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                name = $"{baseName}_{i++}";
            return name;
        }
    }
}
