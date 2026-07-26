using Geometry.Meshing;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// Counts manifold edges whose two incident faces traverse the edge in the same direction (inconsistent winding).
    /// </summary>
    public static class MeshWindingDiagnostics
    {
        public readonly struct EdgeStats
        {
            public int ManifoldEdges { get; init; }
            public int InconsistentManifoldEdges { get; init; }
            public int NonManifoldEdges { get; init; }
            public int BoundaryEdges { get; init; }
        }

        public static EdgeStats Analyze<T>(IReadOnlyMesh<T> mesh) where T : IVertex
        {
            int manifold = 0;
            int inconsistent = 0;
            int nonManifold = 0;
            int boundary = 0;

            foreach (var kvp in mesh.Edges)
            {
                int faceCount = kvp.Value.Faces.Count;
                if (faceCount == 0)
                    continue;
                if (faceCount == 1)
                {
                    boundary++;
                    continue;
                }

                if (faceCount > 2)
                {
                    nonManifold++;
                    continue;
                }

                manifold++;
                IFace[] faces = [.. kvp.Value.Faces];
                bool f0 = TraversesForward(faces[0].iVerts, kvp.Key.A, kvp.Key.B);
                bool f1 = TraversesForward(faces[1].iVerts, kvp.Key.A, kvp.Key.B);
                if (f0 == f1)
                    inconsistent++;
            }

            return new EdgeStats
            {
                ManifoldEdges = manifold,
                InconsistentManifoldEdges = inconsistent,
                NonManifoldEdges = nonManifold,
                BoundaryEdges = boundary
            };
        }

        /// <summary>
        /// Inconsistent manifold edges whose two faces are not both incident to a non-manifold edge.
        /// Non-zero values indicate propagation gaps; non-manifold-only inconsistencies are not fixable by flipping alone.
        /// </summary>
        public static int CountInconsistentAwayFromNonManifold<T>(IReadOnlyMesh<T> mesh) where T : IVertex
        {
            HashSet<IEdgeKey> nonManifoldKeys = [.. mesh.Edges.Where(kvp => kvp.Value.Faces.Count > 2).Select(kvp => kvp.Key)];

            int count = 0;
            foreach (var kvp in mesh.Edges)
            {
                if (kvp.Value.Faces.Count != 2)
                    continue;

                IFace[] faces = [.. kvp.Value.Faces];
                if (TraversesForward(faces[0].iVerts, kvp.Key.A, kvp.Key.B) != TraversesForward(faces[1].iVerts, kvp.Key.A, kvp.Key.B))
                    continue;

                bool touchesNonManifold = faces[0].Edges.Any(nonManifoldKeys.Contains)
                    || faces[1].Edges.Any(nonManifoldKeys.Contains);
                if (touchesNonManifold == false)
                    count++;
            }

            return count;
        }

        private static bool TraversesForward(ImmutableArray<int> iVerts, int a, int b)
        {
            for (int i = 0; i < iVerts.Length; i++)
            {
                int x = iVerts[i];
                int y = iVerts[(i + 1) % iVerts.Length];
                if (x == a && y == b)
                    return true;
                if (x == b && y == a)
                    return false;
            }

            return false;
        }
    }
}
