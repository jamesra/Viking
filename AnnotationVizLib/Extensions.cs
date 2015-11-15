using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnnotationVizLib.AnnotationService;

namespace AnnotationVizLib
{
    static class Extensions
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
