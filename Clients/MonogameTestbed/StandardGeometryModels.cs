using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace MonogameTestbed
{
    public static class StandardGeometryModels
    {
        public static GridVector2[] CreateTestPolygonExteriorVerticies(GridVector2? offset = new GridVector2?())
        {
            GridVector2[] output = new GridVector2[] {new GridVector2(10,10),
                                      new GridVector2(5, 20),
                                      new GridVector2(15, 30),
                                      new GridVector2(30, 30),
                                      new GridVector2(25, 15),
                                      new GridVector2(45, 15),
                                      new GridVector2(45, 10),
                                      new GridVector2(55, 0),
                                      new GridVector2(25, 5),
                                      new GridVector2(10, 10)};
             
            return output;
        }

        public static GridVector2[] CreateTestPolygonInteriorRingVerticies(GridVector2? offset = new GridVector2?())
        {
            GridVector2[] output = new GridVector2[] {new GridVector2(12.5,12.5),
                                      new GridVector2(22.5, 12.5),
                                      new GridVector2(24.5, 17.5),
                                      new GridVector2(17.5, 25.5),
                                      new GridVector2(12.5, 17.5),
                                     new GridVector2(12.5, 12.5)};
             
            return output;
        }

        public static GridPolygon CreateTestPolygon(GridVector2? offset = new GridVector2?())
        {
            GridVector2[] holy_cps = CreateTestPolygonExteriorVerticies();
            GridVector2[] holy_hole = CreateTestPolygonInteriorRingVerticies();
            List<GridVector2[]> listInnerRings = new List<GridVector2[]>();
            listInnerRings.Add(holy_hole);

            //When I made this I did not center polygon on 0,0, so just recenter after creation for now
            GridPolygon uncentered_poly = new GridPolygon(holy_cps, listInnerRings);
            GridPolygon centered_poly = uncentered_poly.Translate(-uncentered_poly.Centroid);

            if (offset.HasValue)
                return centered_poly.Translate(offset.Value);
            else
                return centered_poly;
            
        }
    }
}
