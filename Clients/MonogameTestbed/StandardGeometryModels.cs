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
        public static GridPolygon CreateBoxPolygon(GridRectangle rect)
        {
            GridVector2[] points = new GridVector2[6];

            Array.Copy(rect.Corners, points, 4);
            points[4] = rect.Center;
            points[5] = points[0];

            return new GridPolygon(points);
        }

        public static GridVector2[] CreateTestPolygonExteriorVerticies(GridVector2? offset = new GridVector2?())
        {
            GridVector2[] output = [new(10,10),
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

        public static GridVector2[] CreateTestPolygonInteriorRingVerticies(GridVector2? offset = new GridVector2?())
        {
            GridVector2[] output = [new(12.5,12.5),
                                      new(22.5, 12.5),
                                      new(24.5, 17.5),
                                      new(17.5, 25.5),
                                      new(12.5, 17.5),
                                     new(12.5, 12.5)];

            return output;
        }

        public static GridPolygon CreateTestPolygon(bool IncludeHole, GridVector2? offset = new GridVector2?())
        {
            GridVector2[] holy_cps = CreateTestPolygonExteriorVerticies();
            List<GridVector2[]> listInnerRings = [];

            if (IncludeHole)
            {
                GridVector2[] holy_hole = CreateTestPolygonInteriorRingVerticies();
                listInnerRings.Add(holy_hole);
            }

            //When I made this I did not center polygon on 0,0, so just recenter after creation for now
            GridPolygon uncentered_poly = new(holy_cps, listInnerRings);
            GridPolygon centered_poly = uncentered_poly.Translate(-uncentered_poly.Centroid);

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
