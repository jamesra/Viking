using Geometry;
using Geometry.Meshing;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// Edge-propagation face reorientation for <see cref="Mesh3D{MorphMeshVertex}"/> meshes.
    /// Walks only 2-manifold edges (exactly two faces). Three-face junctions cannot be oriented and
    /// must not join patches; that is what left inconsistent 2-face edges and checkerboard lighting
    /// on the composite. Called from per-slice <see cref="BajajGeneratorMesh.EnsureFacesHaveExternalNormals"/>
    /// and from composite <c>SliceGraphMeshModel.EnsureCompositeWinding</c>.
    /// </summary>
    public static class MeshWindingReorientation
    {
        public readonly struct Options
        {
            /// <summary>When true, medial-axis cap faces are never reversed during propagation.</summary>
            public bool RespectAnchorFaces { get; init; }

            /// <summary>When true, flip each closed component via signed volume after propagation (even if an anchor seed was used).</summary>
            public bool AlwaysOrientOutward { get; init; }

            /// <summary>When false, skip the greedy repair pass (run outward orientation first, then repair separately).</summary>
            public bool RunRepairPass { get; init; }
        }

        public readonly struct Result
        {
            public int BeforeInconsistent { get; init; }
            public int AfterInconsistent { get; init; }
            public int AfterInconsistentAwayFromNonManifold { get; init; }
            public int TotalReversals { get; init; }
            public int ComponentsFlipped { get; init; }
            public int RepairPassReversals { get; init; }
        }

        public static Result Reorient(Mesh3D<MorphMeshVertex> mesh, Options options)
        {
            var beforeStats = MeshWindingDiagnostics.Analyze(mesh);
            int totalReversals = 0;
            int componentsFlipped = 0;
            HashSet<IFace> visited = [];

            foreach (IFace start in mesh.Faces.ToArray())
            {
                if (visited.Contains(start))
                    continue;

                List<IFace> componentFaces = CollectTwoManifoldPatch(mesh, start, visited, options, out List<IFace> anchors);
                List<IFace> seeds = options.RespectAnchorFaces && anchors.Count > 0
                    ? anchors
                    : [componentFaces[0]];
                List<IFace> component = PropagateWindingFromSeeds(mesh, seeds, options, ref totalReversals);

                //Signed volume is only meaningful for closed shells; open morphology components have boundary edges.
                IFace anchorSeed = anchors.Count > 0 ? anchors[0] : null;
                if ((options.AlwaysOrientOutward || anchorSeed is null) && ComponentHasBoundaryEdge(mesh, component) == false)
                {
                    if (OrientComponentOutward(mesh, component))
                        componentsFlipped++;
                }
            }

            int repairPassReversals = options.RunRepairPass
                ? RepairInconsistentManifoldEdges(mesh, options, ref totalReversals)
                : 0;

            var afterStats = MeshWindingDiagnostics.Analyze(mesh);
            return new Result
            {
                BeforeInconsistent = beforeStats.InconsistentManifoldEdges,
                AfterInconsistent = afterStats.InconsistentManifoldEdges,
                AfterInconsistentAwayFromNonManifold = MeshWindingDiagnostics.CountInconsistentAwayFromNonManifold(mesh),
                TotalReversals = totalReversals,
                ComponentsFlipped = componentsFlipped,
                RepairPassReversals = repairPassReversals
            };
        }

        /// <summary>
        /// Faces sharing an edge that has exactly two faces, i.e. a corridor that can be wound opposite.
        /// Edges with any other face count are patch boundaries. Used by Reorient and
        /// <see cref="MorphMeshOutwardOrientation"/>.
        /// </summary>
        public static IEnumerable<(IFace Neighbor, IEdgeKey SharedEdge)> TwoManifoldNeighbors(Mesh3D<MorphMeshVertex> mesh, IFace f)
        {
            foreach (IEdgeKey ek in f.Edges)
            {
                IEdge edge = mesh.Edges[ek];
                if (edge.Faces.Count != 2)
                    continue;

                foreach (IFace nf in edge.Faces)
                {
                    if (f.Equals(nf) == false)
                        yield return (nf, ek);
                }
            }
        }

        /// <summary>Re-run BFS propagation only (no volume flip, no repair).</summary>
        public static int PropagateConsistencyOnly(Mesh3D<MorphMeshVertex> mesh)
        {
            var options = new Options { RespectAnchorFaces = false, AlwaysOrientOutward = false, RunRepairPass = false };
            int totalReversals = 0;
            HashSet<IFace> visited = [];

            foreach (IFace start in mesh.Faces.ToArray())
            {
                if (visited.Contains(start))
                    continue;

                List<IFace> componentFaces = CollectTwoManifoldPatch(mesh, start, visited, options, out List<IFace> anchors);
                List<IFace> seeds = anchors.Count > 0 ? anchors : [componentFaces[0]];
                PropagateWindingFromSeeds(mesh, seeds, options, ref totalReversals);
            }

            return totalReversals;
        }

        /// <summary>Greedy repair of inconsistent manifold edges.</summary>
        public static int RepairManifoldConsistency(Mesh3D<MorphMeshVertex> mesh)
        {
            var options = new Options { RespectAnchorFaces = false };
            int totalReversals = 0;
            return RepairInconsistentManifoldEdges(mesh, options, ref totalReversals);
        }

        private static bool ComponentHasBoundaryEdge(Mesh3D<MorphMeshVertex> mesh, List<IFace> component)
        {
            foreach (IFace f in component)
            {
                foreach (IEdgeKey ek in f.Edges)
                {
                    if (mesh.Edges[ek].Faces.Count == 1)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Greedy pass over remaining inconsistent manifold pairs; converges when only non-manifold junctions remain.
        /// </summary>
        private static int RepairInconsistentManifoldEdges(Mesh3D<MorphMeshVertex> mesh, Options options, ref int totalReversals)
        {
            int repairs = 0;
            for (int pass = 0; pass < 32; pass++)
            {
                int passReversals = 0;
                foreach (KeyValuePair<IEdgeKey, IEdge> kvp in mesh.Edges.ToList())
                {
                    if (kvp.Value.Faces.Count != 2)
                        continue;

                    IFace[] faces = [.. kvp.Value.Faces];
                    if (TraversesForward(faces[0].iVerts, kvp.Key.A, kvp.Key.B)
                        != TraversesForward(faces[1].iVerts, kvp.Key.A, kvp.Key.B))
                        continue;

                    IFace toReverse = IsAnchorFace(mesh, faces[1], options) ? faces[0] : faces[1];
                    if (IsAnchorFace(mesh, toReverse, options))
                        continue;

                    ReverseFace(mesh, toReverse);
                    passReversals++;
                }

                totalReversals += passReversals;
                repairs += passReversals;
                if (passReversals == 0)
                    break;
            }

            return repairs;
        }

        private static bool IsAnchorFace(Mesh3D<MorphMeshVertex> mesh, IFace f, Options options)
        {
            if (options.RespectAnchorFaces == false)
                return false;

            if (f is MorphMeshFace morphFace && morphFace.NormalIsKnownCorrect)
                return true;

            return mesh[f.iVerts].Any(v => v.MedialAxisIndex.HasValue);
        }

        /// <summary>
        /// Flood faces connected to <paramref name="start"/> across 2-manifold edges only.
        /// </summary>
        private static List<IFace> CollectTwoManifoldPatch(
            Mesh3D<MorphMeshVertex> mesh, IFace start, HashSet<IFace> visited, Options options, out List<IFace> anchors)
        {
            anchors = [];
            List<IFace> component = [];
            Queue<IFace> queue = new();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                IFace f = queue.Dequeue();
                component.Add(f);
                if (IsAnchorFace(mesh, f, options))
                    anchors.Add(f);

                foreach ((IFace nf, _) in TwoManifoldNeighbors(mesh, f))
                {
                    if (visited.Contains(nf))
                        continue;

                    visited.Add(nf);
                    queue.Enqueue(nf);
                }
            }

            return component;
        }

        /// <summary>
        /// Breadth-first from every seed at once across 2-manifold edges, reversing any neighbor that
        /// traverses the shared edge in the same direction. Anchor seeds are never reversed.
        /// </summary>
        private static List<IFace> PropagateWindingFromSeeds(
            Mesh3D<MorphMeshVertex> mesh, List<IFace> seeds, Options options, ref int totalReversals)
        {
            List<IFace> component = [];
            HashSet<IFace> placed = [.. seeds];
            Queue<IFace> queue = new();
            foreach (IFace seed in seeds)
                queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                IFace current = queue.Dequeue();
                component.Add(current);

                foreach (IEdgeKey ek in current.Edges)
                {
                    IEdge edge = mesh.Edges[ek];
                    if (edge.Faces.Count != 2)
                        continue;

                    IFace[] neighbors = [.. edge.Faces];
                    foreach (IFace nf in neighbors)
                    {
                        if (current.Equals(nf) || placed.Contains(nf))
                            continue;

                        IFace neighbor = nf;
                        bool currentForward = TraversesForward(current.iVerts, ek.A, ek.B);
                        bool neighborForward = TraversesForward(neighbor.iVerts, ek.A, ek.B);

                        if (currentForward == neighborForward && IsAnchorFace(mesh, neighbor, options) == false)
                        {
                            neighbor = ReverseFace(mesh, neighbor);
                            totalReversals++;
                        }

                        placed.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
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

        private static bool OrientComponentOutward(Mesh3D<MorphMeshVertex> mesh, List<IFace> component)
        {
            if (ComponentSignedVolume(mesh, component) <= 0)
                return false;

            foreach (IFace f in component.ToArray())
                ReverseFace(mesh, f);

            return true;
        }

        private static double ComponentSignedVolume(Mesh3D<MorphMeshVertex> mesh, List<IFace> component)
        {
            double sixV = 0;
            foreach (IFace f in component)
            {
                MorphMeshVertex[] verts = [.. mesh[f.iVerts]];
                for (int i = 1; i + 1 < verts.Length; i++)
                {
                    Vector3 a = verts[0].Position;
                    Vector3 b = verts[i].Position;
                    Vector3 c = verts[i + 1].Position;
                    sixV += Vector3.Dot(a, Vector3.Cross(b, c));
                }
            }

            return sixV / 6.0;
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
