using Geometry;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonogameTestbed
{
    public static class StandardGeometryModels
    {
        public static Polygon CreateBoxPolygon(Rectangle rect)
        {
            Vector2[] points = new Vector2[6];

            Array.Copy(rect.Corners, points, 4);
            points[4] = rect.Center;
            points[5] = points[0];

            return new Polygon(points);
        }

        public static Vector2[] CreateTestPolygonExteriorVerticies(Vector2? offset = new Vector2?())
        {
            Vector2[] output = [new(10,10),
                                      new(5, 20),
                                      new(15, 30),
                                      new(30, 30),
                                      new(25, 15),
                                      new(45, 15),
                                      new(45, 10),
                                      new(55, 0),
                                      new(25, 5),
                                      new(10, 10)];

            return output;
        }

        public static Vector2[] CreateTestPolygonInteriorRingVerticies(Vector2? offset = new Vector2?())
        {
            Vector2[] output = [new(12.5,12.5),
                                      new(22.5, 12.5),
                                      new(24.5, 17.5),
                                      new(17.5, 25.5),
                                      new(12.5, 17.5),
                                     new(12.5, 12.5)];

            return output;
        }

        public static Polygon CreateTestPolygon(bool IncludeHole, Vector2? offset = new Vector2?())
        {
            Vector2[] holy_cps = CreateTestPolygonExteriorVerticies();
            List<Vector2[]> listInnerRings = [];

            if (IncludeHole)
            {
                Vector2[] holy_hole = CreateTestPolygonInteriorRingVerticies();
                listInnerRings.Add(holy_hole);
            }

            //When I made this I did not center polygon on 0,0, so just recenter after creation for now
            Polygon uncentered_poly = new(holy_cps, listInnerRings);
            Polygon centered_poly = uncentered_poly.Translate(-uncentered_poly.Centroid);

            if (offset.HasValue)
                return centered_poly.Translate(offset.Value);
            else
                return centered_poly;

        }
    }

    public enum StandardModel
    {
        PolyOverNotchedBox,
        PolyOverNotchedBoxOffset,
        PolyFourLevelStraightProcess,
        Custom
    }

    /*
    public static class StandardModels
    {
        ... BuildMeshGraph and SharedModel* require SmoothMeshGraphGenerator (not in MorphologyMesh). See commented block below.
    }
    */
}
