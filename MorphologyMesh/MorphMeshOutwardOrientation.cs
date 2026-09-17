using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// Contour-based tests for whether face winding points outward from annotation polygons.
    /// Matches the logic in <see cref="BajajGeneratorMesh.FaceHasCCWWinding"/>.
    /// </summary>
    public static class MorphMeshOutwardOrientation
    {
        public readonly struct ShapeAtZ
        {
            public IShape2D Shape { get; init; }
            public bool IsUpper { get; init; }
            public double Z { get; init; }
        }

        public sealed class ShapeContext
        {
            public IReadOnlyList<ShapeAtZ> ShapesAtZ { get; init; } = [];

            /// <summary>Maps vertex ShapeIndex.ShapeIndex to upper/lower (local index on slice meshes, morph-node index on composite).</summary>
            public IReadOnlyDictionary<int, bool> IsUpperByShapeIndex { get; init; } = new Dictionary<int, bool>();

            public static ShapeContext FromSliceTopology(SliceTopology topology) => new()
            {
                ShapesAtZ = BuildShapesAtZ(topology),
                IsUpperByShapeIndex = BuildIsUpperMapLocal(topology)
            };

            public static ShapeContext FromAccumulated(
                IReadOnlyList<ShapeAtZ> shapesAtZ,
                IReadOnlyDictionary<int, bool> isUpperByShapeIndex) => new()
            {
                ShapesAtZ = shapesAtZ,
                IsUpperByShapeIndex = isUpperByShapeIndex
            };

            private static List<ShapeAtZ> BuildShapesAtZ(SliceTopology topology)
            {
                List<ShapeAtZ> list = new(topology.Shapes.Length);
                for (int i = 0; i < topology.Shapes.Length; i++)
                {
                    list.Add(new ShapeAtZ
                    {
                        Shape = topology.Shapes[i],
                        IsUpper = topology.IsUpper[i],
                        Z = topology.ShapeZ[i]
                    });
                }

                return list;
            }

            /// <summary>Per-slice meshes use local polygon indices in ShapeIndex.ShapeIndex.</summary>
            private static Dictionary<int, bool> BuildIsUpperMapLocal(SliceTopology topology)
            {
                Dictionary<int, bool> map = [];
                for (int i = 0; i < topology.Shapes.Length; i++)
                    map[i] = topology.IsUpper[i];

                return map;
            }

            private ZShapeIndex _upperByZ;
            private ZShapeIndex _lowerByZ;

            /// <summary>
            /// True when any shape on the requested side, within <paramref name="zTol"/> of
            /// <paramref name="faceZ"/>, contains <paramref name="point"/>.
            ///
            /// Equivalent to filtering <see cref="ShapesAtZ"/> linearly, but the caller runs this once per face
            /// and an assembled composite accumulates every contour of every slice: structure 180 pairs ~10^5
            /// faces with ~10^3 shapes, so the linear filter was the dominant cost of the orientation pass.
            /// zTol does not vary per face, so the shapes can be bucketed by Z once and queried by range.
            /// </summary>
            internal bool AnyShapeContains(bool isUpper, double faceZ, double zTol, IPoint2D point)
            {
                ZShapeIndex index = isUpper
                    ? _upperByZ ??= new ZShapeIndex(ShapesAtZ, wantUpper: true)
                    : _lowerByZ ??= new ZShapeIndex(ShapesAtZ, wantUpper: false);

                return index.AnyContains(faceZ, zTol, point);
            }
        }

        /// <summary>
        /// Shapes of one side grouped into ascending buckets of exact Z, so a Z-range query visits only the
        /// buckets it needs.  Bucket keys are the shapes' own Z values, so a range scan selects exactly the
        /// shapes a linear |s.Z - faceZ| &lt;= zTol filter would.
        /// </summary>
        private sealed class ZShapeIndex
        {
            /// <summary>Identity comparer, needed because <see cref="IShape2D"/> implementations refuse to hash.</summary>
            private sealed class ReferenceComparer : IEqualityComparer<IShape2D>
            {
                public static readonly ReferenceComparer Instance = new();

                public bool Equals(IShape2D x, IShape2D y) => ReferenceEquals(x, y);

                public int GetHashCode(IShape2D obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }

            private readonly double[] _z;
            private readonly IShape2D[][] _shapes;

            public ZShapeIndex(IReadOnlyList<ShapeAtZ> shapesAtZ, bool wantUpper)
            {
                var buckets = shapesAtZ
                    .Where(s => s.IsUpper == wantUpper && s.Shape != null)
                    .GroupBy(s => s.Z)
                    .OrderBy(g => g.Key)
                    .ToArray();

                _z = new double[buckets.Length];
                _shapes = new IShape2D[buckets.Length][];
                for (int i = 0; i < buckets.Length; i++)
                {
                    _z[i] = buckets[i].Key;

                    //Adjacent slices contribute the same contour instance more than once, and dropping the repeats
                    //only changes how much work a query does, never its result, since the caller asks whether *any*
                    //shape contains the point.  Deduplication must be by reference: Polygon.GetHashCode() throws
                    //rather than hash geometry that compares with an epsilon.
                    _shapes[i] = [.. buckets[i].Select(s => s.Shape).Distinct(ReferenceComparer.Instance)];
                }
            }

            public bool AnyContains(double faceZ, double zTol, IPoint2D point)
            {
                double minZ = faceZ - zTol;
                double maxZ = faceZ + zTol;

                for (int i = FirstBucketAtOrAfter(minZ); i < _z.Length && _z[i] <= maxZ; i++)
                {
                    foreach (IShape2D shape in _shapes[i])
                    {
                        if (shape.GetRelation(point) == ShapeRelation.Contained)
                            return true;
                    }
                }

                return false;
            }

            private int FirstBucketAtOrAfter(double minZ)
            {
                int lo = 0;
                int hi = _z.Length;
                while (lo < hi)
                {
                    int mid = lo + ((hi - lo) / 2);
                    if (_z[mid] < minZ)
                        lo = mid + 1;
                    else
                        hi = mid;
                }

                return lo;
            }
        }

        /// <summary>
        /// Returns true when the face winding should be reversed so the normal points outward from the contours.
        /// Sidewalls that span two slices must not use the cap-containment test: the face centroid sits
        /// between section Z values, containment misses, and majority vote inverts an already-outward tube.
        /// Called by <see cref="OrientComponentsOutward"/> on per-slice meshes and the assembled composite.
        /// </summary>
        public static bool FaceNeedsFlipForOutward(Mesh3D<MorphMeshVertex> mesh, IFace f, ShapeContext ctx)
        {
            return TryFaceNeedsFlipForOutward(mesh, f, ctx, out bool needsFlip) && needsFlip;
        }

        private static bool TryFaceNeedsFlipForOutward(
            Mesh3D<MorphMeshVertex> mesh, IFace f, ShapeContext ctx, out bool needsFlip)
        {
            needsFlip = false;
            MorphMeshVertex[] verts = [.. mesh[f.iVerts]];
            Vector3 n = mesh.Normal(f);

            if (f.IsTriangle() && IsSidewall(verts, n))
                return TrySidewallNeedsFlip(verts, ctx, out needsFlip);

            Vector2 faceCenter = mesh.GetCentroid(f);
            double faceZ = verts.Average(v => v.Position.Z);
            double zTol = Math.Max(Global.Epsilon * 1000, 0.5);

            //A cap face is outward when it sits over a contour on its own side; if nothing contains it, the
            //normal points into the shape and the winding has to be reversed.
            bool testUpperSide = n.Z >= 0;
            needsFlip = ctx.AnyShapeContains(testUpperSide, faceZ, zTol, (IPoint2D)faceCenter) == false;
            return true;
        }

        /// <summary>
        /// A tiling triangle lives on two sections, or is closer to vertical than to a cap.
        /// Composite faces always span Z; testing them as caps with zTol≈1 always votes "flip".
        /// </summary>
        private static bool IsSidewall(MorphMeshVertex[] verts, Vector3 n)
        {
            double zMin = verts.Min(v => v.Position.Z);
            double zMax = verts.Max(v => v.Position.Z);
            if (zMax - zMin > Global.Epsilon)
                return true;

            return Math.Abs(n.Z) < 0.5;
        }

        /// <summary>
        /// Contour-edge winding test that does not need <see cref="MorphMeshVertex.Corresponding"/> (not
        /// copied onto the composite) or a per-morph-node IsUpper map (each location is upper in one
        /// slice pair and lower in the next). Uses <see cref="IShapeIndex.Equals"/> because boxed
        /// PolygonIndex == is reference equality and never matched Next/Previous.
        /// </summary>
        private static bool TrySidewallNeedsFlip(MorphMeshVertex[] verts, ShapeContext ctx, out bool needsFlip)
        {
            needsFlip = false;
            if (TryGetContourEdge(verts, out MorphMeshVertex onContour, out MorphMeshVertex opposite) == false)
                return false;
            if (onContour.ShapeIndex is null)
                return false;

            bool contourIsUpper = ContourIsUpperOnFace(onContour, opposite, ctx);
            int iOnContour = Array.IndexOf(verts, onContour);
            InfiniteSequentialIndexSet faceIndexer = new(0, verts.Length, 0);
            MorphMeshVertex nextVert = verts[faceIndexer[iOnContour + 1]];
            MorphMeshVertex prevVert = verts[faceIndexer[iOnContour - 1]];

            bool output;
            if (SameShapeIndex(nextVert.ShapeIndex, onContour.ShapeIndex.Next))
                output = contourIsUpper;
            else if (SameShapeIndex(nextVert.ShapeIndex, onContour.ShapeIndex.Previous))
                output = contourIsUpper == false;
            else
                output = SameShapeIndex(prevVert.ShapeIndex, onContour.ShapeIndex.Previous)
                    ? contourIsUpper
                    : contourIsUpper == false;

            needsFlip = onContour.ShapeIndex.IsInner ? !output : output;
            return true;
        }

        /// <summary>
        /// Geometric upper = higher Z on this face. Equal-Z pairing (rare branch grouping) falls back
        /// to the topology map.
        /// </summary>
        private static bool ContourIsUpperOnFace(MorphMeshVertex onContour, MorphMeshVertex opposite, ShapeContext ctx)
        {
            double dz = onContour.Position.Z - opposite.Position.Z;
            if (Math.Abs(dz) > Global.Epsilon)
                return dz > 0;

            if (onContour.ShapeIndex is not null
                && ctx.IsUpperByShapeIndex.TryGetValue(onContour.ShapeIndex.ShapeIndex, out bool mapped))
                return mapped;

            return false;
        }

        private static bool TryGetContourEdge(
            MorphMeshVertex[] verts, out MorphMeshVertex onContour, out MorphMeshVertex opposite)
        {
            onContour = null;
            opposite = null;

            for (int i = 0; i < verts.Length; i++)
            {
                MorphMeshVertex a = verts[i];
                if (a.ShapeIndex is null)
                    continue;

                for (int j = 0; j < verts.Length; j++)
                {
                    if (i == j)
                        continue;

                    MorphMeshVertex b = verts[j];
                    if (b.ShapeIndex is null)
                        continue;

                    if (SameShapeIndex(b.ShapeIndex, a.ShapeIndex.Next) == false
                        && SameShapeIndex(b.ShapeIndex, a.ShapeIndex.Previous) == false)
                        continue;

                    MorphMeshVertex third = verts.FirstOrDefault(v =>
                        ReferenceEquals(v, a) == false && ReferenceEquals(v, b) == false);
                    if (third is null)
                        return false;

                    onContour = a;
                    opposite = third;
                    return true;
                }
            }

            MorphMeshVertex[] ordered = [.. verts.OrderBy(v => v.Position.Z)];
            if (Math.Abs(ordered[0].Position.Z - ordered[1].Position.Z) <= Global.Epsilon
                && ordered[2].Position.Z - ordered[1].Position.Z > Global.Epsilon)
            {
                onContour = ordered[0].ShapeIndex is not null ? ordered[0] : ordered[1];
                opposite = ordered[2];
                return onContour.ShapeIndex is not null;
            }

            if (Math.Abs(ordered[1].Position.Z - ordered[2].Position.Z) <= Global.Epsilon
                && ordered[1].Position.Z - ordered[0].Position.Z > Global.Epsilon)
            {
                onContour = ordered[1].ShapeIndex is not null ? ordered[1] : ordered[2];
                opposite = ordered[0];
                return onContour.ShapeIndex is not null;
            }

            return false;
        }

        private static bool SameShapeIndex(IShapeIndex a, IShapeIndex b) =>
            a is not null && b is not null && a.Equals(b);

        /// <summary>
        /// After manifold-consistent winding, flip 2-manifold patches that mostly point inward relative to contours.
        /// Uses majority vote per patch instead of a single cap face (critical for large merged composites).
        /// Patches are the same 2-manifold components <see cref="MeshWindingReorientation"/> orients; walking
        /// through 3-face junctions would mix independently orientable sides and invert the wrong walls.
        /// </summary>
        public static int OrientComponentsOutward(Mesh3D<MorphMeshVertex> mesh, ShapeContext ctx)
        {
            int componentsFlipped = 0;
            HashSet<IFace> visited = [];

            foreach (IFace start in mesh.Faces.ToArray())
            {
                if (visited.Contains(start))
                    continue;

                List<IFace> component = CollectTwoManifoldPatch(mesh, start, visited);
                int needFlip = 0;
                int sampled = 0;
                foreach (IFace f in component)
                {
                    if (f.IsTriangle() == false)
                        continue;
                    if (TryFaceNeedsFlipForOutward(mesh, f, ctx, out bool flip) == false)
                        continue;

                    sampled++;
                    if (flip)
                        needFlip++;
                }

                if (sampled == 0)
                    continue;

                if (needFlip > sampled / 2)
                {
                    foreach (IFace f in component.ToArray())
                        ReverseFace(mesh, f);
                    componentsFlipped++;
                }
            }

            return componentsFlipped;
        }

        private static List<IFace> CollectTwoManifoldPatch(Mesh3D<MorphMeshVertex> mesh, IFace start, HashSet<IFace> visited)
        {
            List<IFace> component = [];
            Queue<IFace> queue = new();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                IFace f = queue.Dequeue();
                component.Add(f);
                foreach ((IFace nf, _) in MeshWindingReorientation.TwoManifoldNeighbors(mesh, f))
                {
                    if (visited.Contains(nf))
                        continue;
                    visited.Add(nf);
                    queue.Enqueue(nf);
                }
            }

            return component;
        }

        private static IFace ReverseFace(Mesh3D<MorphMeshVertex> mesh, IFace f)
        {
            if (mesh is MorphRenderMesh morph)
                return morph.ReverseFace(f);

            mesh.RemoveFace(f);
            IFace newFace = mesh.CreateFace?.Invoke(f.iVerts.Reverse()) ?? Face.Create(f.iVerts.Reverse());
            mesh.AddFace(newFace);
            return newFace;
        }
    }
}
