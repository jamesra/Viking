using Geometry;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;

namespace MonogameTestbed
{
    public static class MeshingExperimentExtensions
    {
        public static TriangleNet.Voronoi.VoronoiBase ConvexHullVoronoi(List<GridVector2[]> ConvexHulls)
        {
            List<TriangleNet.Geometry.Vertex> verts = new List<TriangleNet.Geometry.Vertex>();
            List<int> IndexMap = new List<int>();

            for (int i = 0; i < ConvexHulls.Count; i++)
            {
                GridVector2[] set = ConvexHulls[i];
                verts.AddRange(set.Select(p =>
                {
                    var v = new TriangleNet.Geometry.Vertex(p.X, p.Y, i, 1);
                    v.Attributes[0] = i;
                    return v;
                }));

                IndexMap.AddRange(set.Select(p => i));
            }

            if (verts.Count >= 4)
            {
                var Voronoi = verts.Voronoi();
                for (int i = 0; i < IndexMap.Count; i++)
                {
                    Voronoi.Vertices[i].Label = IndexMap[i];
                }

                return Voronoi;
            }

            return null;
        }
        
    }


}
