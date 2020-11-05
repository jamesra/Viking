using AnnotationService.Types;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib.WCFClient
{
    static class WCFLocationExtensions
    {
        public static List<ObjAttribute> Attributes(this Location loc)
        {
            return ObjAttribute.Parse(loc.AttributesXml);
        }

        public static bool IsVericosityCap(this Location loc)
        {
            List<ObjAttribute> attribs = loc.Attributes();
            return attribs.Any(a => a.Name == "Vericosity Cap");
        }

        public static bool IsUntraceable(this Location loc)
        {
            List<ObjAttribute> attribs = loc.Attributes();
            return attribs.Any(a => a.Name == "Untraceable");
        }

    }
}
