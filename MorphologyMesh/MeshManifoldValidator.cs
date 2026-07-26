using Geometry.Meshing;
using System.Collections.Immutable;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// The manifold state of a generated mesh.
    ///
    /// A per-slice Bajaj mesh is not expected to be closed: its CONTOUR edges are the seam shared with the
    /// adjacent slice and legitimately carry a single face until the slices are composited.  Boundary edges are
    /// therefore split into that expected seam and everything else, which represents a genuine hole.
    /// </summary>
    public readonly struct MeshManifoldReport
    {
        public int FaceCount { get; init; }

        /// <summary>Edges shared by exactly two faces.</summary>
        public int ManifoldEdges { get; init; }

        /// <summary>Edges shared by three or more faces.  Always a defect.</summary>
        public int NonManifoldEdges { get; init; }

        /// <summary>Edges whose two faces traverse the edge in the same direction, so their normals disagree.</summary>
        public int InconsistentManifoldEdges { get; init; }

        /// <summary>Edges carrying no face at all.  Usually a chord that never became part of a triangle.</summary>
        public int IsolatedEdges { get; init; }

        /// <summary>Single-face CONTOUR edges.  Expected on a per-slice mesh, a hole on a composite.</summary>
        public int ContourBoundaryEdges { get; init; }

        /// <summary>Single-face edges that are not contour seams.  Always a hole in the surface.</summary>
        public int UnexpectedBoundaryEdges { get; init; }

        /// <summary>True when no edge has more than two faces.</summary>
        public bool IsEdgeManifold => NonManifoldEdges == 0;

        /// <summary>True when every two-face edge is traversed in opposite directions by its faces.</summary>
        public bool IsConsistentlyOriented => InconsistentManifoldEdges == 0;

        /// <summary>True when the surface has no holes beyond the expected contour seams.</summary>
        public bool IsFreeOfUnexpectedHoles => UnexpectedBoundaryEdges == 0;

        /// <summary>True when the surface is watertight, with no boundary edges of any kind.</summary>
        public bool IsClosed => IsEdgeManifold && IsConsistentlyOriented && UnexpectedBoundaryEdges == 0 && ContourBoundaryEdges == 0;

        /// <summary>
        /// The state a per-slice mesh should reach: no non-manifold edges, no disagreeing normals, and no holes
        /// other than the contour seam that the adjacent slice will close.
        /// </summary>
        public bool IsValidSliceSurface => IsEdgeManifold && IsConsistentlyOriented && IsFreeOfUnexpectedHoles;

        public override string ToString() =>
            $"faces:{FaceCount} manifold:{ManifoldEdges} nonManifold:{NonManifoldEdges} inconsistent:{InconsistentManifoldEdges} " +
            $"contourSeam:{ContourBoundaryEdges} holes:{UnexpectedBoundaryEdges} isolated:{IsolatedEdges}";
    }

    /// <summary>
    /// Measures whether a mesh satisfies the 2-manifold invariant the Bajaj reconstruction is supposed to produce.
    /// Nothing else in the pipeline enforced this, so defects propagated silently into the composite and the export.
    /// </summary>
    public static class MeshManifoldValidator
    {
        public static MeshManifoldReport Validate<T>(IReadOnlyMesh<T> mesh) where T : IVertex
        {
            int manifold = 0;
            int nonManifold = 0;
            int inconsistent = 0;
            int isolated = 0;
            int contourBoundary = 0;
            int unexpectedBoundary = 0;

            foreach (var kvp in mesh.Edges)
            {
                int faceCount = kvp.Value.Faces.Count;

                if (faceCount == 0)
                {
                    isolated++;
                    continue;
                }

                if (faceCount == 1)
                {
                    if (kvp.Value is MorphMeshEdge morphEdge && morphEdge.Type == EdgeType.CONTOUR)
                        contourBoundary++;
                    else
                        unexpectedBoundary++;
                    continue;
                }

                if (faceCount > 2)
                {
                    nonManifold++;
                    continue;
                }

                manifold++;

                IFace[] faces = [.. kvp.Value.Faces];
                if (TraversesForward(faces[0].iVerts, kvp.Key.A, kvp.Key.B) == TraversesForward(faces[1].iVerts, kvp.Key.A, kvp.Key.B))
                    inconsistent++;
            }

            return new MeshManifoldReport
            {
                FaceCount = mesh.Faces.Count,
                ManifoldEdges = manifold,
                NonManifoldEdges = nonManifold,
                InconsistentManifoldEdges = inconsistent,
                IsolatedEdges = isolated,
                ContourBoundaryEdges = contourBoundary,
                UnexpectedBoundaryEdges = unexpectedBoundary
            };
        }

        /// <summary>
        /// Returns true when the closed ring of vertex indicies traverses the directed edge a to b.
        /// </summary>
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
