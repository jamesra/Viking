using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// Edge-propagation face reorientation for <see cref="Mesh3D{MorphMeshVertex}"/> meshes.
    /// Walks only 2-manifold edges (exactly two faces). Three-face junctions cannot be oriented and
    /// must not join patches; that is what left inconsistent 2-face edges and checkerboard lighting
    /// on the composite. Called from per-slice <see cref="BajajGeneratorMesh.EnsureFacesHaveExternalNormals"/>
    /// and from composite <c>SliceGraphMeshModel.EnsureCompositeWinding</c>.
    /// Mutations run only after planning finishes. With <see cref="Options.SkipFailedPatches"/> false
    /// (the default) a late failure leaves the original mesh unchanged. Slice and composite callers
    /// turn that on so a Möbius patch is skipped, other patches still reorient, and generated faces
    /// are kept instead of discarding the whole slice.
    /// </summary>
    public static class MeshWindingReorientation
    {
        public const int MaxRayAttempts = 32;

        public readonly struct Options
        {
            /// <summary>When true, medial-axis cap faces are never reversed during propagation.</summary>
            public bool RespectAnchorFaces { get; init; }

            /// <summary>When true, flip each closed component via signed volume after propagation (even if an anchor seed was used).</summary>
            public bool AlwaysOrientOutward { get; init; }

            /// <summary>When false, skip the greedy repair pass (run outward orientation first, then repair separately).</summary>
            public bool RunRepairPass { get; init; }

            /// <summary>
            /// Orient one seed face per 2-manifold patch by ray casting from a known contour-interior point, then
            /// propagate that winding through the patch. Used for polygon and circle surfaces, not open ribbons.
            /// </summary>
            public bool SeedByInteriorRay { get; init; }

            /// <summary>Interior points retained by slice generators, with shape indices in the mesh's current index space.</summary>
            public IReadOnlyList<WindingInteriorSeed> InteriorSeeds { get; init; }

            /// <summary>
            /// Shape indices whose faces form cullable polygon surfaces. Patches containing only other indices are
            /// open sheets and receive consistency propagation without an outward ray.
            /// </summary>
            public IReadOnlySet<int> OutwardShapeIndices { get; init; }

            /// <summary>Slice or composite identity included in the required failure message after 32 attempts.</summary>
            public string FailureContext { get; init; }

            /// <summary>
            /// When true, a non-orientable or unseedable patch is skipped so other patches still reorient.
            /// Slice generation sets this so a Möbius cycle does not discard the rest of the slice.
            /// Unit tests leave it false (the default) so those failures still throw.
            /// </summary>
            public bool SkipFailedPatches { get; init; }
        }

        public readonly struct Result
        {
            public int BeforeInconsistent { get; init; }
            public int AfterInconsistent { get; init; }
            public int AfterInconsistentAwayFromNonManifold { get; init; }
            public int TotalReversals { get; init; }
            public int ComponentsFlipped { get; init; }
            public int RepairPassReversals { get; init; }

            /// <summary>Messages from patches skipped because they were non-orientable or could not be seeded.</summary>
            public IReadOnlyList<string> PatchFailures { get; init; }
        }

        public static Result Reorient(Mesh3D<MorphMeshVertex> mesh, Options options)
        {
            var beforeStats = MeshWindingDiagnostics.Analyze(mesh);
            List<PatchPlan> plans = Plan(mesh, options, out List<string> patchFailures);

            int totalReversals = 0;
            int componentsFlipped = 0;
            foreach (PatchPlan plan in plans)
            {
                foreach (IFace face in plan.FacesToReverse)
                {
                    ReverseFace(mesh, face);
                    totalReversals++;
                }

                if (plan.ComponentFlippedByVolume)
                    componentsFlipped++;
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
                RepairPassReversals = repairPassReversals,
                PatchFailures = patchFailures
            };
        }

        /// <summary>
        /// Faces sharing an edge that has exactly two faces, i.e. a corridor that can be wound opposite.
        /// Edges with any other face count, and zero-length corresponding edges (same-Z stacked verts),
        /// are patch boundaries. Used by Reorient and <see cref="MorphMeshOutwardOrientation"/>.
        /// </summary>
        public static IEnumerable<(IFace Neighbor, IEdgeKey SharedEdge)> TwoManifoldNeighbors(Mesh3D<MorphMeshVertex> mesh, IFace f)
        {
            foreach (IEdgeKey ek in f.Edges)
            {
                if (IsOrientableManifoldEdge(mesh, ek) == false)
                    continue;

                IEdge edge = mesh.Edges[ek];
                foreach (IFace nf in edge.Faces)
                {
                    if (f.Equals(nf) == false)
                        yield return (nf, ek);
                }
            }
        }

        /// <summary>
        /// A 2-face edge only orients a patch when it has length. Same-Z corresponding verts collapse to a
        /// point; walking through them glued two zero-area fans into a patch no ray could hit.
        /// </summary>
        private static bool IsOrientableManifoldEdge(Mesh3D<MorphMeshVertex> mesh, IEdgeKey ek)
        {
            IEdge edge = mesh.Edges[ek];
            if (edge.Faces.Count != 2)
                return false;

            return Vector3.DistanceSquared(mesh[ek.A].Position, mesh[ek.B].Position) > Global.EpsilonSquared;
        }

        /// <summary>Every 2-manifold patch in the mesh. Used when committing one winding seed per orientable patch.</summary>
        public static List<List<IFace>> CollectTwoManifoldPatches(Mesh3D<MorphMeshVertex> mesh)
        {
            var options = new Options();
            HashSet<IFace> visited = [];
            List<List<IFace>> patches = [];
            foreach (IFace start in mesh.Faces)
            {
                if (visited.Contains(start))
                    continue;

                patches.Add(CollectTwoManifoldPatch(mesh, start, visited, options, out _));
            }

            return patches;
        }

        /// <summary>Re-run BFS propagation only (no volume flip, no repair).</summary>
        public static int PropagateConsistencyOnly(Mesh3D<MorphMeshVertex> mesh)
        {
            var options = new Options { RespectAnchorFaces = false, AlwaysOrientOutward = false, RunRepairPass = false };
            return Reorient(mesh, options).TotalReversals;
        }

        /// <summary>Greedy repair of inconsistent manifold edges.</summary>
        public static int RepairManifoldConsistency(Mesh3D<MorphMeshVertex> mesh)
        {
            var options = new Options { RespectAnchorFaces = false };
            int totalReversals = 0;
            return RepairInconsistentManifoldEdges(mesh, options, ref totalReversals);
        }

        private readonly record struct PatchPlan(
            HashSet<IFace> FacesToReverse,
            bool ComponentFlippedByVolume);

        private static List<PatchPlan> Plan(Mesh3D<MorphMeshVertex> mesh, Options options, out List<string> patchFailures)
        {
            IReadOnlyDictionary<int, WindingInteriorSeed[]> interiorSeedsByShape = options.SeedByInteriorRay
                && options.InteriorSeeds is not null
                ? options.InteriorSeeds.GroupBy(seed => seed.ShapeIndex)
                    .ToDictionary(group => group.Key, group => group.ToArray())
                : null;

            List<PatchPlan> plans = [];
            patchFailures = [];
            HashSet<IFace> visited = [];

            foreach (IFace start in mesh.Faces.ToArray())
            {
                if (visited.Contains(start))
                    continue;

                List<IFace> componentFaces = CollectTwoManifoldPatch(mesh, start, visited, options, out List<IFace> anchors);
                HashSet<IFace> facesToReverse = [];
                bool flippedByVolume = false;

                try
                {
                    if (options.SeedByInteriorRay && ComponentRequiresOutwardSeed(mesh, componentFaces, options.OutwardShapeIndices))
                    {
                        IFace authority = PlanSeedByInteriorRay(
                            mesh, componentFaces, options, interiorSeedsByShape, facesToReverse);
                        PlanPropagation(mesh, [authority], options, facesToReverse);
                    }
                    else
                    {
                        List<IFace> seeds = options.RespectAnchorFaces && anchors.Count > 0
                            ? anchors
                            : [componentFaces[0]];
                        PlanPropagation(mesh, seeds, options, facesToReverse);

                        IFace anchorSeed = anchors.Count > 0 ? anchors[0] : null;
                        if ((options.AlwaysOrientOutward || anchorSeed is null)
                            && ComponentHasBoundaryEdge(mesh, componentFaces) == false)
                        {
                            if (PlannedSignedVolume(mesh, componentFaces, facesToReverse) > 0)
                            {
                                InvertPlannedFlips(componentFaces, facesToReverse);
                                flippedByVolume = true;
                            }
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    if (options.SkipFailedPatches == false)
                        throw;

                    patchFailures.Add(ex.Message);
                    continue;
                }

                plans.Add(new PatchPlan(facesToReverse, flippedByVolume));
            }

            return plans;
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

        private static bool ComponentRequiresOutwardSeed(
            Mesh3D<MorphMeshVertex> mesh,
            List<IFace> component,
            IReadOnlySet<int> outwardShapeIndices)
        {
            if (outwardShapeIndices is null)
                return true;

            return component
                .SelectMany(face => face.iVerts)
                .Select(index => mesh[index].ShapeIndex?.ShapeIndex)
                .Any(index => index.HasValue && outwardShapeIndices.Contains(index.Value));
        }

        private readonly record struct RayHit(IFace Face, double Distance, double NormalDotRay, bool IsGraze, bool IsTangent);
        private enum RayAttemptFailure
        {
            None,
            Miss,
            Graze,
            Tangent
        }

        /// <summary>
        /// Record whether the first unambiguous crossing must flip, without mutating the mesh.
        /// Closed shells use crossing parity so an outside origin still orients outward. Open
        /// patches treat a validated local seed as interior and use only that first exit.
        /// </summary>
        private static IFace PlanSeedByInteriorRay(
            Mesh3D<MorphMeshVertex> mesh,
            List<IFace> component,
            Options options,
            IReadOnlyDictionary<int, WindingInteriorSeed[]> seedsByShape,
            HashSet<IFace> facesToReverse)
        {
            if (options.InteriorSeeds is null || options.InteriorSeeds.Count == 0)
                ThrowRaySeedFailure(component, options, "no interior seed was recorded");

            WindingInteriorSeed originSeed = SelectInteriorSeed(mesh, component, seedsByShape, options);
            Vector3 origin = originSeed.Position;
            bool closed = ComponentHasBoundaryEdge(mesh, component) == false;
            double scale = ComponentScale(mesh, component);
            if (closed == false && SeedIsLocalToPatch(origin, mesh, component, scale) == false)
            {
                ThrowRaySeedFailure(component, options,
                    $"open-patch seed is not local to the patch; seed shape={originSeed.ShapeIndex} at {origin}");
            }

            Vector3 target = ClosestTriangleCentroid(mesh, component, origin);
            Vector3 firstDirection = target - origin;
            int patchSeed = StablePatchSeed(origin, originSeed.ShapeIndex);
            int misses = 0;
            int grazes = 0;
            int tangents = 0;

            for (int attempt = 0; attempt < MaxRayAttempts; attempt++)
            {
                Vector3 direction = DirectionForAttempt(attempt, firstDirection, patchSeed);
                Ray3D ray = new(origin, direction);
                if (TryFindFirstOrientationHit(
                    mesh, component, ray, scale, closed,
                    out RayHit hit, out int crossingCount, out RayAttemptFailure failure) == false)
                {
                    misses += failure == RayAttemptFailure.Miss ? 1 : 0;
                    grazes += failure == RayAttemptFailure.Graze ? 1 : 0;
                    tangents += failure == RayAttemptFailure.Tangent ? 1 : 0;
                    continue;
                }

                bool originIsInside = closed == false || (crossingCount & 1) == 1;
                bool seedPointsOutward = originIsInside ? hit.NormalDotRay > 0 : hit.NormalDotRay < 0;
                if (seedPointsOutward == false)
                    facesToReverse.Add(hit.Face);

                return hit.Face;
            }

            Vector3 boundsMin = component.SelectMany(face => face.iVerts).Select(index => mesh[index].Position)
                .Aggregate((a, b) => new Vector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)));
            Vector3 boundsMax = component.SelectMany(face => face.iVerts).Select(index => mesh[index].Position)
                .Aggregate((a, b) => new Vector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
            string reason =
                $"all {MaxRayAttempts} rays failed (miss={misses}, graze={grazes}, tangent={tangents}); " +
                $"seed shape={originSeed.ShapeIndex} at {origin}; patch bounds={boundsMin}..{boundsMax}";

            // Open islands (same-Z corresponding fans, leftover 2-face caps) have no outward shell.
            // Throwing here discarded the rest of the slice in ConvertToMesh; SkipFailedPatches callers
            // keep the faces and propagate consistency from the first face instead.
            if (closed == false && options.SkipFailedPatches)
            {
                Trace.WriteLine($"Open patch ray seed failed; using consistency-only for {options.FailureContext}: {reason}");
                return component[0];
            }

            ThrowRaySeedFailure(component, options, reason);
            return null;
        }

        /// <summary>
        /// Aim the deterministic ray through a triangle interior. Aiming at a contour vertex guarantees the first
        /// attempt is a graze; the nearest centroid also keeps small disconnected patches from subtending a tiny
        /// solid angle.
        /// </summary>
        private static Vector3 ClosestTriangleCentroid(
            Mesh3D<MorphMeshVertex> mesh,
            List<IFace> component,
            Vector3 origin)
        {
            Vector3 closest = default;
            double closestDistance = double.PositiveInfinity;
            foreach (IFace face in component)
            {
                if (face.iVerts.Length < 3)
                    continue;

                Vector3 a = mesh[face.iVerts[0]].Position;
                for (int i = 1; i + 1 < face.iVerts.Length; i++)
                {
                    Vector3 centroid = (a + mesh[face.iVerts[i]].Position + mesh[face.iVerts[i + 1]].Position) / 3.0;
                    double distance = Vector3.DistanceSquared(origin, centroid);
                    if (distance < closestDistance)
                    {
                        closest = centroid;
                        closestDistance = distance;
                    }
                }
            }

            return closest;
        }

        /// <summary>Require a seed whose shape occurs in this patch. Unrelated mesh seeds are never used.</summary>
        private static WindingInteriorSeed SelectInteriorSeed(
            Mesh3D<MorphMeshVertex> mesh,
            List<IFace> component,
            IReadOnlyDictionary<int, WindingInteriorSeed[]> seedsByShape,
            Options options)
        {
            HashSet<int> componentShapes = [.. component
                .SelectMany(face => face.iVerts)
                .Select(index => mesh[index].ShapeIndex?.ShapeIndex)
                .Where(index => index.HasValue)
                .Select(index => index.Value)];

            WindingInteriorSeed[] matching = [.. componentShapes
                .Where(shape => seedsByShape is not null && seedsByShape.ContainsKey(shape))
                .SelectMany(shape => seedsByShape[shape])];
            if (matching.Length == 0)
            {
                ThrowRaySeedFailure(component, options,
                    "no interior seed whose shape occurs in this patch");
            }

            Vector3 center = Vector3.Zero;
            int count = 0;
            foreach (int index in component.SelectMany(face => face.iVerts).Distinct())
            {
                center += mesh[index].Position;
                count++;
            }

            center /= Math.Max(1, count);
            return matching.MinBy(seed => Vector3.DistanceSquared(seed.Position, center));
        }

        private static int StablePatchSeed(Vector3 origin, int shapeIndex)
        {
            unchecked
            {
                int hash = shapeIndex * 397;
                hash = (hash * 397) ^ origin.X.GetHashCode();
                hash = (hash * 397) ^ origin.Y.GetHashCode();
                hash = (hash * 397) ^ origin.Z.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Attempt 0 aims at the nearest triangle centroid. Later attempts walk a Fibonacci sphere
        /// rotated by a per-patch seed so the same patch always retries the same directions.
        /// </summary>
        internal static Vector3 DirectionForAttempt(int attempt, Vector3 firstDirection, int patchSeed)
        {
            if (attempt == 0)
            {
                double length = Vector3.Magnitude(firstDirection);
                if (length > Global.Epsilon)
                    return firstDirection / length;
                return new Vector3(0, 0, 1);
            }

            const double goldenAngle = 2.399963229728653;
            double t = (attempt - 0.5) / (MaxRayAttempts - 1);
            double y = 1.0 - (t * 2.0);
            double radius = Math.Sqrt(Math.Max(0, 1.0 - (y * y)));
            double theta = (goldenAngle * attempt) + (patchSeed * 0.6180339887498948);
            return new Vector3(Math.Cos(theta) * radius, Math.Sin(theta) * radius, y);
        }

        private static bool SeedIsLocalToPatch(
            Vector3 origin,
            Mesh3D<MorphMeshVertex> mesh,
            List<IFace> component,
            double scale)
        {
            double nearest = double.PositiveInfinity;
            foreach (int index in component.SelectMany(face => face.iVerts).Distinct())
            {
                double distance = Vector3.DistanceSquared(origin, mesh[index].Position);
                if (distance < nearest)
                    nearest = distance;
            }

            // A matching-shape seed belongs with this patch when it sits within one patch-diagonal
            // of some vertex. That accepts mid-band Delaunay insets on flat sheets and still
            // rejects an origin far outside the reconstructed surface.
            return Math.Sqrt(nearest) <= Math.Max(scale, WindingRayTolerances.WorldDistance(scale) * 2.0);
        }

        private static double ComponentScale(Mesh3D<MorphMeshVertex> mesh, List<IFace> component)
        {
            Vector3 min = new(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
            Vector3 max = new(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
            foreach (int index in component.SelectMany(face => face.iVerts))
            {
                Vector3 p = mesh[index].Position;
                min = new Vector3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new Vector3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }

            double diagonal = Vector3.Magnitude(max - min);
            return diagonal > Global.Epsilon ? diagonal : 1.0;
        }

        /// <summary>
        /// Accept a ray when the nearest crossing cluster is a non-grazing hit. Farther grazes do not
        /// reject that first exit. Closed shells still count later unambiguous crossings for parity.
        /// </summary>
        private static bool TryFindFirstOrientationHit(
            Mesh3D<MorphMeshVertex> mesh,
            List<IFace> component,
            Ray3D ray,
            double scale,
            bool closed,
            out RayHit first,
            out int crossingCount,
            out RayAttemptFailure failure)
        {
            double worldTol = WindingRayTolerances.WorldDistance(scale);
            List<RayHit> hits = [];

            foreach (IFace face in component)
            {
                if (face.iVerts.Length < 3)
                    continue;

                Vector3 a = mesh[face.iVerts[0]].Position;
                for (int i = 1; i + 1 < face.iVerts.Length; i++)
                {
                    Vector3 b = mesh[face.iVerts[i]].Position;
                    Vector3 c = mesh[face.iVerts[i + 1]].Position;
                    if (RayIntersection.TryIntersectTriangle(ray, a, b, c, out double distance, out double u, out double v) == false
                        || distance <= worldTol)
                    {
                        continue;
                    }

                    double w = 1.0 - u - v;
                    bool graze = Math.Min(w, Math.Min(u, v)) <= WindingRayTolerances.BarycentricGraze;
                    Vector3 normal = mesh.Normal([face.iVerts[0], face.iVerts[i], face.iVerts[i + 1]]);
                    double normalLength = Vector3.Magnitude(normal);
                    if (normalLength <= Global.Epsilon)
                        continue;

                    double normalDotRay = Vector3.Dot(normal / normalLength, ray.Direction);
                    bool tangent = Math.Abs(normalDotRay) <= WindingRayTolerances.AngularCosineTangent;
                    hits.Add(new RayHit(face, distance, normalDotRay, graze, tangent));
                }
            }

            if (hits.Count == 0)
            {
                first = default;
                crossingCount = 0;
                failure = RayAttemptFailure.Miss;
                return false;
            }

            RayHit[] ordered = [.. hits.OrderBy(hit => hit.Distance)];
            List<List<RayHit>> clusters = [[ordered[0]]];
            for (int i = 1; i < ordered.Length; i++)
            {
                double clusterTol = Math.Max(worldTol, Math.Abs(ordered[i].Distance) * 1e-9);
                if (Math.Abs(ordered[i].Distance - clusters[^1][0].Distance) <= clusterTol)
                    clusters[^1].Add(ordered[i]);
                else
                    clusters.Add([ordered[i]]);
            }

            List<RayHit> firstCluster = clusters[0];
            if (firstCluster.Any(hit => hit.IsTangent))
            {
                first = default;
                crossingCount = 0;
                failure = RayAttemptFailure.Tangent;
                return false;
            }

            if (firstCluster.Any(hit => hit.IsGraze) || firstCluster.Count != 1)
            {
                first = default;
                crossingCount = 0;
                failure = RayAttemptFailure.Graze;
                return false;
            }

            first = firstCluster[0];
            if (closed == false)
            {
                crossingCount = 1;
                failure = RayAttemptFailure.None;
                return true;
            }

            int crossings = 0;
            foreach (List<RayHit> cluster in clusters)
            {
                if (cluster.Any(hit => hit.IsTangent))
                {
                    crossingCount = 0;
                    failure = RayAttemptFailure.Tangent;
                    return false;
                }

                if (cluster.Any(hit => hit.IsGraze) || cluster.Count != 1)
                {
                    crossingCount = 0;
                    failure = RayAttemptFailure.Graze;
                    return false;
                }

                crossings++;
            }

            crossingCount = crossings;
            failure = RayAttemptFailure.None;
            return true;
        }

        private static void ThrowRaySeedFailure(List<IFace> component, Options options, string reason)
        {
            string context = string.IsNullOrWhiteSpace(options.FailureContext) ? "unknown mesh" : options.FailureContext;
            string message = $"Help me: could not seed outward winding for {context}; patch faces={component.Count}: {reason}.";
            Trace.WriteLine(message);
            throw new InvalidOperationException(message);
        }

        private static void ThrowNonOrientablePatch(List<IFace> component, Options options, IEdgeKey edge)
        {
            string context = string.IsNullOrWhiteSpace(options.FailureContext) ? "unknown mesh" : options.FailureContext;
            string message =
                $"Non-orientable patch: contradictory winding assignments around a cycle in {context}; " +
                $"faces={component.Count}, conflict at edge {edge.A}-{edge.B}.";
            Trace.WriteLine(message);
            throw new InvalidOperationException(message);
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
        /// Breadth-first from every seed across 2-manifold edges, recording reversals without mutating.
        /// A neighbor already assigned the opposite flip is a non-orientable cycle.
        /// </summary>
        private static void PlanPropagation(
            Mesh3D<MorphMeshVertex> mesh,
            List<IFace> seeds,
            Options options,
            HashSet<IFace> facesToReverse)
        {
            HashSet<IFace> placed = [.. seeds];
            Queue<IFace> queue = new();
            foreach (IFace seed in seeds)
                queue.Enqueue(seed);

            List<IFace> component = [];

            while (queue.Count > 0)
            {
                IFace current = queue.Dequeue();
                component.Add(current);
                bool currentFlipped = facesToReverse.Contains(current);

                foreach (IEdgeKey ek in current.Edges)
                {
                    IEdge edge = mesh.Edges[ek];
                    if (IsOrientableManifoldEdge(mesh, ek) == false)
                        continue;

                    foreach (IFace nf in edge.Faces)
                    {
                        if (current.Equals(nf))
                            continue;

                        bool currentForward = TraversesForward(current.iVerts, ek.A, ek.B) != currentFlipped;
                        bool neighborForwardOriginal = TraversesForward(nf.iVerts, ek.A, ek.B);
                        bool neighborShouldFlip = currentForward == neighborForwardOriginal;

                        if (placed.Contains(nf))
                        {
                            bool assignedFlip = facesToReverse.Contains(nf);
                            if (assignedFlip != neighborShouldFlip)
                                ThrowNonOrientablePatch(component, options, ek);
                            continue;
                        }

                        if (neighborShouldFlip && IsAnchorFace(mesh, nf, options))
                            ThrowNonOrientablePatch(component, options, ek);

                        if (neighborShouldFlip)
                            facesToReverse.Add(nf);

                        placed.Add(nf);
                        queue.Enqueue(nf);
                    }
                }
            }
        }

        private static void InvertPlannedFlips(List<IFace> component, HashSet<IFace> facesToReverse)
        {
            HashSet<IFace> inverted = [];
            foreach (IFace face in component)
            {
                if (facesToReverse.Contains(face) == false)
                    inverted.Add(face);
            }

            facesToReverse.Clear();
            foreach (IFace face in inverted)
                facesToReverse.Add(face);
        }

        private static double PlannedSignedVolume(
            Mesh3D<MorphMeshVertex> mesh,
            List<IFace> component,
            HashSet<IFace> facesToReverse)
        {
            double sixV = 0;
            foreach (IFace f in component)
            {
                int[] verts = facesToReverse.Contains(f)
                    ? [.. f.iVerts.Reverse()]
                    : [.. f.iVerts];
                for (int i = 1; i + 1 < verts.Length; i++)
                {
                    Vector3 a = mesh[verts[0]].Position;
                    Vector3 b = mesh[verts[i]].Position;
                    Vector3 c = mesh[verts[i + 1]].Position;
                    sixV += Vector3.Dot(a, Vector3.Cross(b, c));
                }
            }

            return sixV / 6.0;
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
