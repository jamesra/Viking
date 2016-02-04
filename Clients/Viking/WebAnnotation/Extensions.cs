using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity.Spatial;
using RTree;
using Geometry;

namespace WebAnnotation
{
    public static class ColorExtensions
    {
        public static Microsoft.Xna.Framework.Color ToXNAColor(this System.Drawing.Color color)
        {
            return new Microsoft.Xna.Framework.Color((int)color.R,
                                                    (int)color.G,
                                                    (int)color.B,
                                                    (int)color.A);
        }

        public static Microsoft.Xna.Framework.Color ToXNAColor(this System.Drawing.Color color, float alpha)
        {
            return new Microsoft.Xna.Framework.Color((int)color.R,
                                                    (int)color.G,
                                                    (int)color.B,
                                                    (int)(255f * alpha));
        }
    }

    public static class AnnotationExtensions
    {
        private static WebAnnotationModel.LocationType StringToLocationType(string annotationType)
        {
            switch (annotationType)
            {
                case "Circle":
                    return WebAnnotationModel.LocationType.CIRCLE;
                case "ClosedCurve":
                    return WebAnnotationModel.LocationType.CLOSEDCURVE;
                case "OpenCurve":
                    return WebAnnotationModel.LocationType.OPENCURVE;
                case "Polygon":
                    return WebAnnotationModel.LocationType.POLYGON;
                case "Polyline":
                    return WebAnnotationModel.LocationType.POLYLINE;
                case "Point":
                    return WebAnnotationModel.LocationType.POINT;
                case "Ellipse":
                    return WebAnnotationModel.LocationType.ELLIPSE;
                default:
                    return WebAnnotationModel.LocationType.CIRCLE;
            }

            throw new ArgumentException("Unknown annotation type " + annotationType);
        }

        public static WebAnnotationModel.LocationType GetLocationType(this connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd.CreateStructureCommandAction command)
        {
            return StringToLocationType(command.AnnotationType);
        }

        public static WebAnnotationModel.LocationType GetLocationType(this connectomes.utah.edu.XSD.WebAnnotationUserSettings.xsd.ChangeLocationAnnotationTypeAction command)
        {
            return StringToLocationType(command.AnnotationType);
        }
    }

    public static class GeometryExtensions
    {
        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, float Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, int Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, (float)Z, (float)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, float Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, int Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, (float)Z, (float)Z);
        } 
    } 
}
