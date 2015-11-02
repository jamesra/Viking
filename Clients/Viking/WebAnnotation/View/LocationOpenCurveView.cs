using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    class LocationOpenCurveView : LocationLineView
    {
        public LocationOpenCurveView(LocationObj obj) : base(obj) { }
    }
}
