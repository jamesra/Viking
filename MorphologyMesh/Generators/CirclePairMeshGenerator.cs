using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// Tiles a slice that is exactly one CIRCLE annotation per section by lofting matching samples, instead of
    /// running Bajaj on polygons.
    ///
    /// Two linked circles are a frustum between ordered rings. This path leaves contours in annotation XY and
    /// zippers the rings together using the shorter available cross-ring edge at each step.
    ///
    /// Called from <see cref="BajajMeshGenerator.GenerateFaces"/> before the polygon path.  Forks, mixed
    /// polygon/circle slices, and unlinked pairs fall through to Bajaj.
    /// </summary>
    public static class CirclePairMeshGenerator
    {
        /// <summary>
        /// Build the ruled faces between the two CIRCLE rings when this slice is an exclusive linked pair.
        /// </summary>
        /// <returns>True when faces were added and Bajaj must not run. False leaves the mesh untouched.</returns>
        public static bool TryGenerateFaces(BajajGeneratorMesh mesh)
        {
            if (SliceTopology.IsExclusiveCirclePair(mesh.Topology) == false)
                return false;

            int iUpper = mesh.IsUpperShape[0] ? 0 : 1;
            int iLower = 1 - iUpper;

            int[] upper = RingVertexIndices(mesh, iUpper);
            int[] lower = RingVertexIndices(mesh, iLower);
            if (upper is null || lower is null)
                return false;

            LoftRings(mesh, upper, lower);

            foreach (MorphMeshEdge edge in mesh.MorphEdges.Where(e => e.Type == EdgeType.UNKNOWN))
                edge.Type = mesh[edge.A].Position.XY() == mesh[edge.B].Position.XY() ? EdgeType.CORRESPONDING : EdgeType.SURFACE;

            Trace.WriteLine($"Mesh {mesh}: lofted circle pair as a ruled frustum of {mesh.Faces.Count} faces.");
            return true;
        }

        private static int[] RingVertexIndices(BajajGeneratorMesh mesh, int shape)
        {
            if (mesh.Shapes[shape] is not Polygon poly)
                return null;

            SortedDictionary<int, int> byVertex = [];
            foreach (MorphMeshVertex vertex in mesh.MorphVerticies)
            {
                if (vertex.ShapeIndex is PolygonIndex index
                    && index.ShapeIndex == shape
                    && index.InnerShapeIndex is null)
                {
                    byVertex[index.VertexIndex] = vertex.Index;
                }
            }

            int n = poly.ExteriorRing.Length - 1;
            if (n < 3 || byVertex.Count != n)
                return null;

            int[] indices = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (byVertex.TryGetValue(i, out int meshIndex) == false)
                    return null;
                indices[i] = meshIndex;
            }

            return indices;
        }

        private static void LoftRings(BajajGeneratorMesh mesh, int[] upper, int[] lower)
        {
            int lowerStart = Enumerable.Range(0, lower.Length)
                .MinBy(i => Vector3.DistanceSquared(mesh[upper[0]].Position, mesh[lower[i]].Position));
            int upperStep = 0;
            int lowerStep = 0;

            while (upperStep < upper.Length || lowerStep < lower.Length)
            {
                int upperCurrent = upper[upperStep % upper.Length];
                int lowerCurrent = lower[(lowerStart + lowerStep) % lower.Length];
                bool advanceUpper;

                if (upperStep == upper.Length - 1 && lowerStep < lower.Length - 1)
                {
                    advanceUpper = false;
                }
                else if (lowerStep == lower.Length - 1 && upperStep < upper.Length - 1)
                {
                    advanceUpper = true;
                }
                else if (lowerStep == lower.Length)
                {
                    advanceUpper = true;
                }
                else if (upperStep == upper.Length)
                {
                    advanceUpper = false;
                }
                else
                {
                    int upperNext = upper[(upperStep + 1) % upper.Length];
                    int lowerNext = lower[(lowerStart + lowerStep + 1) % lower.Length];
                    advanceUpper = Vector3.DistanceSquared(mesh[upperNext].Position, mesh[lowerCurrent].Position)
                        <= Vector3.DistanceSquared(mesh[upperCurrent].Position, mesh[lowerNext].Position);
                }

                if (advanceUpper)
                {
                    int upperNext = upper[(upperStep + 1) % upper.Length];
                    mesh.AddFace(new MorphMeshFace(upperCurrent, upperNext, lowerCurrent));
                    upperStep++;
                }
                else
                {
                    int lowerNext = lower[(lowerStart + lowerStep + 1) % lower.Length];
                    mesh.AddFace(new MorphMeshFace(upperCurrent, lowerNext, lowerCurrent));
                    lowerStep++;
                }
            }
        }
    }
}
