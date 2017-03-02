using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ODataClient.ConnectomeDataModel;

namespace AnnotationVizLib.OData
{
    static class ODataLocationExtensions
    {
        public static List<ObjAttribute> Attributes(this Location loc)
        {
            return ObjAttribute.Parse(loc.Tags);
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
