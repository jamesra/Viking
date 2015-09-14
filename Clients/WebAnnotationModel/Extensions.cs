using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using WebAnnotationModel.Service;


namespace WebAnnotationModel
{
    public static class GeometryExtensions
    {
        public static GridRectangle ToGridRectangle(WebAnnotationModel.Service.BoundingRectangle bbox)
        {
            return new GridRectangle(bbox.XMin, bbox.XMax, bbox.YMin, bbox.YMax);
        }

        public static WebAnnotationModel.Service.BoundingRectangle ToGridRectangle(GridRectangle rect)
        {
            return new BoundingRectangle() { XMin = rect.Left, XMax = rect.Right, YMin = rect.Bottom, YMax = rect.Top };
        }
    }
}
