using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Single-face edges bordering a deliberate polyline fork gap.  Expected, not a hole.
        ///
        /// Where a polyline forks to two partners, one contour segment between the partners' vertex ranges is left
        /// untiled on purpose so the fork reads as a fork.  The edges around that gap carry one face each and would
        /// otherwise be indistinguishable from a tear in the surface.
        /// </summary>
        public int PolylineForkBoundaryEdges { get; init; }

        /// <summary>
        /// Single-face edges on the legitimate boundary of an open polyline ribbon: the chord at each end of the
        /// sheet and the outline of a tapered end cap.  Expected, not a hole.
        ///
        /// A ribbon between open curves is a sheet, not a tube: its boundary is the two contours (the seam the
        /// adjacent slices close) plus one chord at each end.  Nothing will ever put a second face on those, so
        /// counting them as holes flagged every gap-junction slice as an invalid surface.
        /// </summary>
        public int RibbonBoundaryEdges { get; init; }

        /// <summary>
        /// Cross-band polyline pairs joined by exactly one triangle.  Two polylines on different sections should
        /// share a full quad or nothing; a lone triangle is a sliver.  The one legitimate exception, an annotation
        /// consisting of a single point, is not implemented and so cannot occur.
        /// </summary>
        public int SingleTrianglePolylinePairs { get; init; }

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
            $"contourSeam:{ContourBoundaryEdges} holes:{UnexpectedBoundaryEdges} isolated:{IsolatedEdges} " +
            $"forkGap:{PolylineForkBoundaryEdges} ribbonEdge:{RibbonBoundaryEdges} singleTriPolyline:{SingleTrianglePolylinePairs}";
    }

    /// <summary>
    /// One edge that breaks the slice-surface invariant: <c>nonManifold</c> (3+ faces), <c>hole</c> (single face,
    /// not a contour seam), <c>isolated</c> (no face) or <c>inconsistent</c> (two faces with disagreeing winding).
    /// </summary>
    public sealed record MeshManifoldDefect(string Kind, int A, int B, string EdgeType, int FaceCount,
        Geometry.Vector3 PositionA, Geometry.Vector3 PositionB, string ShapeA, string ShapeB, string Faces)
    {
        public override string ToString() =>
            $"{Kind} {EdgeType} faces:{FaceCount} v{A}[{ShapeA}] ({PositionA.X:F1},{PositionA.Y:F1},{PositionA.Z:F1}) - v{B}[{ShapeB}] ({PositionB.X:F1},{PositionB.Y:F1},{PositionB.Z:F1}) {Faces}";
    }

    /// <summary>
    /// Measures whether a mesh satisfies the 2-manifold invariant the Bajaj reconstruction is supposed to produce.
    /// Nothing else in the pipeline enforced this, so defects propagated silently into the composite and the export.
    /// </summary>
    public static class MeshManifoldValidator
    {
        /// <param name="isForkGapBoundary">Identifies single-face edges that border a deliberate polyline fork gap,
        /// so they are counted separately instead of as holes.  Null reports every non-contour boundary edge as a
        /// hole, which is the behavior for meshes with no fork information.</param>
        /// <param name="singleTrianglePolylinePairs">Count of cross-band polyline pairs sharing exactly one face,
        /// which the caller measures because it needs shape identity rather than just edges.</param>
        /// <param name="isRibbonBoundary">Identifies single-face edges on the legitimate boundary of an open polyline
        /// ribbon (end chords, cap taper outline).  Null reports those as holes, which is correct for a polygon mesh
        /// where every boundary should close.</param>
        public static MeshManifoldReport Validate<T>(IReadOnlyMesh<T> mesh, Func<IEdgeKey, bool> isForkGapBoundary = null, int singleTrianglePolylinePairs = 0, Func<IEdgeKey, bool> isRibbonBoundary = null) where T : IVertex
        {
            int manifold = 0;
            int nonManifold = 0;
            int inconsistent = 0;
            int isolated = 0;
            int contourBoundary = 0;
            int unexpectedBoundary = 0;
            int forkBoundary = 0;
            int ribbonBoundary = 0;

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
                    else if (isForkGapBoundary is not null && isForkGapBoundary(kvp.Key))
                        forkBoundary++;
                    else if (isRibbonBoundary is not null && isRibbonBoundary(kvp.Key))
                        ribbonBoundary++;
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

                if (IsCollapsedEdge(mesh, kvp.Key))
                    continue;

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
                UnexpectedBoundaryEdges = unexpectedBoundary,
                PolylineForkBoundaryEdges = forkBoundary,
                RibbonBoundaryEdges = ribbonBoundary,
                SingleTrianglePolylinePairs = singleTrianglePolylinePairs
            };
        }

        /// <summary>
        /// Lists the individual edges behind the counts in <see cref="Validate"/>, so a failing slice can be traced to a
        /// place in the annotation rather than just a tally.  Contour seams are not defects and are not listed.
        /// </summary>
        /// <param name="maxDefects">Cap on the number of entries; a badly broken mesh can have thousands.</param>
        /// <param name="isAnchored">Marks faces the winding repair was not allowed to flip.  Defaults to the face's
        /// current <see cref="MorphMeshFace.NormalIsKnownCorrect"/>, which the final normals pass sets on every face,
        /// so a caller that wants the pre-repair state must capture it beforehand and pass it here.</param>
        public static List<MeshManifoldDefect> DescribeDefects(MorphRenderMesh mesh, Func<IEdgeKey, bool> isForkGapBoundary = null,
            Func<IEdgeKey, bool> isRibbonBoundary = null, int maxDefects = 200, Func<IFace, bool> isAnchored = null)
        {
            isAnchored ??= f => f is MorphMeshFace mf && mf.NormalIsKnownCorrect;
            List<MeshManifoldDefect> defects = [];

            foreach (var kvp in mesh.Edges)
            {
                if (defects.Count >= maxDefects)
                    break;

                int faceCount = kvp.Value.Faces.Count;
                string kind;
                if (faceCount == 0)
                {
                    kind = "isolated";
                }
                else if (faceCount == 1)
                {
                    if (kvp.Value is MorphMeshEdge morphEdge && morphEdge.Type == EdgeType.CONTOUR)
                        continue;
                    if (isForkGapBoundary is not null && isForkGapBoundary(kvp.Key))
                        continue;
                    if (isRibbonBoundary is not null && isRibbonBoundary(kvp.Key))
                        continue;
                    kind = "hole";
                }
                else if (faceCount > 2)
                {
                    kind = "nonManifold";
                }
                else
                {
                    if (IsCollapsedEdge(mesh, kvp.Key))
                        continue;

                    IFace[] faces = [.. kvp.Value.Faces];
                    if (TraversesForward(faces[0].iVerts, kvp.Key.A, kvp.Key.B) != TraversesForward(faces[1].iVerts, kvp.Key.A, kvp.Key.B))
                        continue;
                    kind = "inconsistent";
                }

                MorphMeshVertex a = mesh[kvp.Key.A];
                MorphMeshVertex b = mesh[kvp.Key.B];
                string edgeType = kvp.Value is MorphMeshEdge me ? me.Type.ToString() : "?";
                //An anchored face (NormalIsKnownCorrect) is one the winding repair will not flip, so knowing which
                //faces on an inconsistent edge are anchored says whether the repair could ever have fixed it.
                string faceList = string.Join(" ", kvp.Value.Faces.Select(f =>
                    $"[{string.Join(",", f.iVerts)}]{(isAnchored(f) ? "A" : "")}"));
                defects.Add(new MeshManifoldDefect(kind, kvp.Key.A, kvp.Key.B, edgeType, faceCount,
                    a.Position, b.Position, DescribeVertex(a), DescribeVertex(b), faceList));
            }

            return defects;
        }

        private static string DescribeVertex(MorphMeshVertex v)
        {
            if (v.ShapeIndex is not null)
                return v.ShapeIndex.ToString();
            if (v.MedialAxisIndex.HasValue)
                return $"medial {v.MedialAxisIndex.Value}";
            return "?";
        }

        /// <summary>
        /// Same-Z corresponding verts collapse to a point. Winding around a zero-length edge is not a
        /// surface defect; MeshWindingReorientation already treats those edges as patch boundaries.
        /// </summary>
        private static bool IsCollapsedEdge<T>(IReadOnlyMesh<T> mesh, IEdgeKey key) where T : IVertex
        {
            if (mesh[key.A] is not IVertex3D a || mesh[key.B] is not IVertex3D b)
                return false;

            return Vector3.DistanceSquared(a.Position, b.Position) <= Global.EpsilonSquared;
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
