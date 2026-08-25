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
        }

        /// <summary>
        /// Returns true when the face winding should be reversed so the normal points outward from the contours.
        /// </summary>
        public static bool FaceNeedsFlipForOutward(Mesh3D<MorphMeshVertex> mesh, IFace f, ShapeContext ctx)
        {
            MorphMeshVertex[] verts = [.. mesh[f.iVerts]];
            Vector3 n = mesh.Normal(f);
            double faceZ = verts.Average(v => v.Position.Z);

            if (Math.Abs(n.Z) < Global.Epsilon)
            {
                if (f.IsTriangle() == false)
                    return false;

                MorphMeshVertex noncorresponding = verts.First(v =>
                    v.Corresponding.HasValue == false || f.iVerts.Contains(v.Corresponding.Value) == false);

                if (noncorresponding.ShapeIndex is null)
                    return false;

                if (ctx.IsUpperByShapeIndex.TryGetValue(noncorresponding.ShapeIndex.ShapeIndex, out bool nonCorrespondingIsUpper) == false)
                    return false;

                int iNonCorresponding = Array.IndexOf(verts, noncorresponding);
                InfiniteSequentialIndexSet faceIndexer = new(0, f.iVerts.Length, 0);

                MorphMeshVertex nextVert = verts[faceIndexer[iNonCorresponding + 1]];
                MorphMeshVertex prevVert = verts[faceIndexer[iNonCorresponding - 1]];

                bool output;
                if (nextVert.ShapeIndex == noncorresponding.ShapeIndex.Next)
                    output = nonCorrespondingIsUpper == false;
                else if (nextVert.ShapeIndex == noncorresponding.ShapeIndex.Previous)
                    output = nonCorrespondingIsUpper;
                else
                    output = prevVert.ShapeIndex == noncorresponding.ShapeIndex.Previous
                        ? nonCorrespondingIsUpper == false
                        : nonCorrespondingIsUpper;

                return noncorresponding.ShapeIndex.IsInner ? !output : output;
            }

            Vector2 faceCenter = mesh.GetCentroid(f);
            double zTol = Math.Max(Global.Epsilon * 1000, 0.5);

            if (n.Z < 0)
            {
                IEnumerable<IShape2D> lowersAtZ = ctx.ShapesAtZ
                    .Where(s => s.IsUpper == false && Math.Abs(s.Z - faceZ) <= zTol)
                    .Select(s => s.Shape);
                if (lowersAtZ.Any(p => p.GetRelation((IPoint2D)faceCenter) == ShapeRelation.Contained))
                    return false;
                return true;
            }

            IEnumerable<IShape2D> uppersAtZ = ctx.ShapesAtZ
                .Where(s => s.IsUpper && Math.Abs(s.Z - faceZ) <= zTol)
                .Select(s => s.Shape);
            if (uppersAtZ.Any(p => p.GetRelation((IPoint2D)faceCenter) == ShapeRelation.Contained))
                return false;
            return true;
        }

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

                    sampled++;
                    if (FaceNeedsFlipForOutward(mesh, f, ctx))
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
