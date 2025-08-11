using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MaterRevitAddin.Utils
{
    public static class MaterialParamFinder
    {
        public static (List<Parameter> instanceParams, List<Parameter> typeParams) GetEditableMaterialParams(Document doc, Element e)
        {
            var inst = e.Parameters.Cast<Parameter>().Where(IsEditableMaterialParam).ToList();
            var typeElem = doc.GetElement(e.GetTypeId()) as Element;
            var typeList = typeElem?.Parameters.Cast<Parameter>().Where(IsEditableMaterialParam).ToList() ?? new List<Parameter>();
            return (inst, typeList);
        }

        static bool IsEditableMaterialParam(Parameter p)
        {
            if (p == null || p.IsReadOnly) return false;
            var dt = p.Definition.GetDataType();
            return dt == SpecTypeId.Material && p.StorageType == StorageType.ElementId;
        }
    }
}
