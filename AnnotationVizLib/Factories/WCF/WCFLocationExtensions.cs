using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationVizLib.AnnotationService;

namespace AnnotationVizLib
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
