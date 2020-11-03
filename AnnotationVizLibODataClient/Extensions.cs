using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AnnotationVizLib.OData
{ 
   
    public static class ODataExtensions
    {
        public static UnitsAndScale.Scale ToGeometryScale(this ODataClient.Geometry.Scale scale)
        {
            return new UnitsAndScale.Scale(new UnitsAndScale.AxisUnits(scale.X.Value, scale.X.Units),
                                      new UnitsAndScale.AxisUnits(scale.Y.Value, scale.Y.Units),
                                      new UnitsAndScale.AxisUnits(scale.Z.Value, scale.Z.Units));
        }
    }
}
