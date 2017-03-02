using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationVizLib.WCFClient
{ 
    public static class WCFExtensions
    {
        public static Geometry.Scale ToGeometryScale(this AnnotationVizLib.WCFClient.AnnotationService.Scale scale)
        {
            return new Geometry.Scale(new Geometry.AxisUnits(scale.X.Value, scale.X.Units),
                                        new Geometry.AxisUnits(scale.Y.Value, scale.Y.Units),
                                        new Geometry.AxisUnits(scale.Z.Value, scale.Z.Units));
        }
    } 
}
