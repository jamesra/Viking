using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnnotationVizLib
{
    public struct GMLAttribute
    {
        public string Type;
        public string Default;

        public GMLAttribute(string type, string d)
        {
            this.Type = type;
            this.Default = d;
        }
    }
    public static class GMLAttributes
    {
        public static SortedDictionary<string, GMLAttribute> GMLTypeForAttribute = new SortedDictionary<string, GMLAttribute>()
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
