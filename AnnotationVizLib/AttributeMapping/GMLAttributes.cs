using System.Collections.Generic;

namespace AnnotationVizLib
{
    public struct GMLAttribute(string type, string d)
    {
        public string Type = type;
        public string Default = d;
    }
    public static class GMLAttributes
    {
        public static SortedDictionary<string, GMLAttribute> GMLTypeForAttribute = new()
        {
            {"Label", new GMLAttribute("string", null) },
            {"Radius", new GMLAttribute("double","0")},
            {"NumLinkedStructures", new GMLAttribute("int", null)},
            {"LocationID", new GMLAttribute("long",null)},
            {"ParentID", new GMLAttribute("long",null)},
            {"edgeType", new GMLAttribute("string",null) }
        };
    }
}
