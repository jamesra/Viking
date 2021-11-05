using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebAnnotationModel.WCF
{
    public static class Extensions
    {
        public static AnnotationService.Types.BoundingRectangle ToBoundingRectangle(this GridRectangle rect)
        {
            return new AnnotationService.Types.BoundingRectangle() { XMin = rect.Left, XMax = rect.Right, YMin = rect.Bottom, YMax = rect.Top };
        }

        public static GridRectangle ToGridRectangle(this AnnotationService.Types.BoundingRectangle bbox)
        {
            return new GridRectangle(bbox.XMin, bbox.XMax, bbox.YMin, bbox.YMax);
        }
    }
}
