using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AnnotationVizLib.OData
{ 
   
    public static class ODataExtensions
    {
        public static Geometry.Scale ToGeometryScale(this ODataClient.Geometry.Scale scale)
        {
            return new Geometry.Scale(new Geometry.AxisUnits(scale.X.Value, scale.X.Units),
                                      new Geometry.AxisUnits(scale.Y.Value, scale.Y.Units),
                                      new Geometry.AxisUnits(scale.Z.Value, scale.Z.Units));
        }
    }
}
