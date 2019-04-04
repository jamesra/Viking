using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using AnnotationVizLib;
using SqlGeometryUtils;
using Geometry;

namespace XNAModelBuilder
{
    public class MorphologyModelBuilder
    {
        Geometry.Scale Scale;
        public MorphologyModelBuilder(MorphologyGraph graph, Geometry.Scale scale)
        {
            this.Scale = scale; 
        }

        public SimpleMesh GetMesh(MorphologyNode node)
        {
            GridVector2[] points = node.Geometry.ToPoints();
            GridVector3[] upperPoints = points.Select(p => new GridVector3(p.X, p.Y, node.Z + (Scale.Z.Value / 2.0))).ToArray();
            GridVector3[] lowerPoints = points.Select(p => new GridVector3(p.X, p.Y, node.Z - (Scale.Z.Value / 2.0))).ToArray();

            GridVector3[] verts = new GridVector3[points.Length * 2];
            for(int iv = 0; iv < points.Length; iv++)
            {
                verts[iv] = upperPoints[iv];
                verts[iv + points.Length] = lowerPoints[iv];
            }

            int[] edges = new int[points.Count() * 6];
            int iVert = 0;
            //Create vertex array and indicate edges
            for (int iEdge = 0; iEdge < edges.Length; iEdge += 6)
            {
                edges[iEdge + 0] = iVert;
                edges[iEdge + 1] = iVert + 1;
                edges[iEdge + 2] = iVert + points.Length;

                edges[iEdge + 3] = iVert + points.Length;
                edges[iEdge + 4] = iVert + 1;
                edges[iEdge + 5] = iVert + points.Length + 1;

                iVert += 1;
            }

            //Some of the edge indicies will be too large.  Loop through and wrap the indicies to the earlier values
            for(int iEdge = edges.Length - 6; iEdge < edges.Length; iEdge++)
            {
                edges[iEdge] = edges[iEdge] >= points.Length ? edges[iEdge] - points.Length : edges[iEdge];
            }

            return new SimpleMesh(verts, edges);
        }
    }
}
