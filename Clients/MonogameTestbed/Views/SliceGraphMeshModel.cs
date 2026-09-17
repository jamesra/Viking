using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    /// <summary>
    /// Builds a single merged mesh from all of the completed slices of a slice graph.
    /// Merges append into growable working buffers; a throttled (~25 Hz) snapshot is published to
    /// <see cref="model"/> so Draw can read without holding <see cref="ModelLock"/>.
    /// </summary>
    public class SliceGraphMeshModel : IColorView
    {
        /// <summary>Minimum time between live publishes during assembly (~25 Hz).</summary>
        static readonly long PublishIntervalTicks = Stopwatch.Frequency / 25;

        /// <summary>
        /// The composite mesh. Not thread safe or protected by ModelLock.
        /// </summary>
        public Mesh3D<MorphMeshVertex> composite = new();

        /// <summary>
        /// Published GPU-facing model. Arrays are swapped atomically on publish; do not mutate in place
        /// from the draw thread. Color edits go through <see cref="EditWorkingVertexColors"/>.
        /// </summary>
        public MeshModel<VertexPositionNormalColor> model = new();

        private readonly Dictionary<IShapeIndex, int> ShapeIndexToVertex = [];

        readonly List<MorphMeshOutwardOrientation.ShapeAtZ> _shapesAtZ = [];
        readonly Dictionary<int, bool> _isUpperByMorphShape = [];

        readonly List<VertexPositionNormalColor> _workingVerts = [];
        readonly List<int> _workingEdges = [];
        long _lastPublishTimestamp;
        int _publishDirty;

        public ReaderWriterLockSlim ModelLock = new();

        /// <summary>
        /// The manifold state of the merged composite, measured after the winding pass. A correct reconstruction
        /// is closed: every slice seam is shared by two faces once its neighbor has been merged in.
        /// </summary>
        public MeshManifoldReport CompositeManifoldReport { get; private set; }

        private Color _color = Color.CornflowerBlue;
        public Color Color
        {
            get => _color;
            set
            {
                if (value == _color)
                    return;

                _color = value;
                try
                {
                    ModelLock.EnterWriteLock();
                    for (int i = 0; i < _workingVerts.Count; i++)
                    {
                        VertexPositionNormalColor v = _workingVerts[i];
                        v.Color = value;
                        _workingVerts[i] = v;
                    }

                    PublishUnlocked(force: true);
                }
                finally
                {
                    ModelLock.ExitWriteLock();
                }
            }
        }

        public float Alpha
        {
            get => Color.GetAlpha();
            set => Color = Color.SetAlpha(value);
        }

        /// <summary>
        /// Slice mesh render model. Vertices are in volume coordinates; keep model transform at origin
        /// so live view and exported geometry share the same placement.
        /// </summary>
        public SliceGraphMeshModel()
        {
        }

        /// <summary>Bake structure color at construction so leaf merges never recolor every vertex later.</summary>
        public SliceGraphMeshModel(Color color)
        {
            _color = color;
        }

        /// <summary>
        /// </summary>
        /// <param name="mesh"></param>
        public void AddSlice(BajajGeneratorMesh mesh)
        {
            using var _phase = MeshPhaseTimings.Measure(MeshPhase.MergeAddSlice, mesh.Vertices.Count);

            AccumulateSliceTopology(mesh.Topology);

            int[] mesh_to_global = new int[mesh.Vertices.Count];

            List<VertexPositionNormalColor> modelVerts = new(mesh.Vertices.Count);

            for (int iVert = 0; iVert < mesh.Vertices.Count; iVert++)
            {
                MorphMeshVertex vertex = mesh[iVert];

                if (vertex.ShapeIndex is null)
                {
                    MorphMeshVertex composite_vertex = MorphMeshVertex.Duplicate(vertex);
                    int iNewVert = composite.AddVertex(composite_vertex);

                    modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color));

                    mesh_to_global[iVert] = iNewVert;
                }
                else
                {
                    ulong iShape = mesh.Topology.ShapeIndexToMorphNodeIndex[vertex.ShapeIndex.ShapeIndex];
                    MorphMeshVertex composite_vertex = MorphMeshVertex.Reindex(vertex, (int)iShape);

                    if (false == ShapeIndexToVertex.TryGetValue(composite_vertex.ShapeIndex, out int iGlobalVert))
                    {
                        iGlobalVert = composite.AddVertex(composite_vertex);
                        ShapeIndexToVertex.Add(composite_vertex.ShapeIndex, iGlobalVert);

                        modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color));
                    }

                    mesh_to_global[iVert] = iGlobalVert;
                }
            }

            AddEdgesToComposite(mesh.Edges.Keys, mesh_to_global);

            int[] NewModelEdges = AddFacesToComposite(mesh.Faces, mesh_to_global);

            using (MeshPhaseTimings.Measure(MeshPhase.MergeNormals, composite.Vertices.Count))
                composite.RecalculateNormals(mesh_to_global);

            UpdateModel(modelVerts, NewModelEdges, mesh_to_global);
        }

        private Geometry.Meshing.Edge[] AddEdgesToComposite(IEnumerable<IEdgeKey> edges, int[] mesh_to_global)
        {
            Edge[] newEdges = [.. edges.Select(k => new Edge(mesh_to_global[k.A], mesh_to_global[k.B]))];
            foreach (Edge composite_edge in newEdges)
            {
                composite.AddEdge(composite_edge);
            }

            return newEdges;
        }

        private int[] AddFacesToComposite(SortedSet<IFace> faces, int[] mesh_to_global)
        {
            Face[] composite_faces = new Face[faces.Count];
            int[] NewModelEdges = new int[faces.Count * 3];

            int iCompositeFace = 0;

            int iModelFace = 0;
            foreach (Face f in faces.Cast<Face>())
            {
                int[] iMapped = new int[f.iVerts.Length];
                for (int i = 0; i < f.iVerts.Length; i++)
                    iMapped[i] = mesh_to_global[f.iVerts[i]];

                Face composite_face = new(iMapped);
                composite_faces[iCompositeFace] = composite_face;

                Array.Copy(iMapped, 0, NewModelEdges, iModelFace, iMapped.Length);

                iModelFace += iMapped.Length;
                iCompositeFace += 1;
            }

            composite.AddFaces(composite_faces);

            return NewModelEdges;
        }

        private void AccumulateSliceTopology(SliceTopology topology)
        {
            for (int i = 0; i < topology.Shapes.Length; i++)
            {
                int morphShape = (int)topology.ShapeIndexToMorphNodeIndex[i];
                _isUpperByMorphShape[morphShape] = topology.IsUpper[i];

                _shapesAtZ.Add(new MorphMeshOutwardOrientation.ShapeAtZ
                {
                    Shape = topology.Shapes[i],
                    IsUpper = topology.IsUpper[i],
                    Z = topology.ShapeZ[i]
                });
            }
        }

        private void MergeAccumulatedSliceTopology(SliceGraphMeshModel other)
        {
            _shapesAtZ.AddRange(other._shapesAtZ);

            foreach (var kvp in other._isUpperByMorphShape)
                _isUpperByMorphShape[kvp.Key] = kvp.Value;
        }

        /// <summary>
        /// Reorient the merged composite so adjacent faces agree across slice boundaries, then refresh GPU normals.
        /// Per-slice meshes are oriented locally; merging can leave thousands of inconsistent shared edges.
        /// Greedy manifold repair is skipped when any edge still has three or more faces: on those composites
        /// the repair oscillates and punches culling holes in the tube.
        /// </summary>
        public void EnsureCompositeWinding()
        {
            if (composite.Faces.Count == 0)
                return;

            using var _phase = MeshPhaseTimings.Measure(MeshPhase.RootFinalize, composite.Vertices.Count);

            var options = new MeshWindingReorientation.Options
            {
                RespectAnchorFaces = false,
                AlwaysOrientOutward = false,
                RunRepairPass = false
            };

            var result = MeshWindingReorientation.Reorient(composite, options);

            var outwardCtx = MorphMeshOutwardOrientation.ShapeContext.FromAccumulated(_shapesAtZ, _isUpperByMorphShape);
            int outwardFlips = MorphMeshOutwardOrientation.OrientComponentsOutward(composite, outwardCtx);

            int repairAfterOutward = 0;
            var afterOutward = MeshWindingDiagnostics.Analyze(composite);
            if (afterOutward.NonManifoldEdges == 0)
                repairAfterOutward = MeshWindingReorientation.RepairManifoldConsistency(composite);

            composite.RecalculateNormals();

            //Build the published arrays outside the lock, then swap once.
            int[] newEdges = [.. composite.Faces.SelectMany(f => f.iVerts)];
            VertexPositionNormalColor[] newVerts = new VertexPositionNormalColor[composite.Vertices.Count];
            for (int i = 0; i < newVerts.Length; i++)
            {
                MorphMeshVertex cv = composite[i];
                Color color = i < _workingVerts.Count ? _workingVerts[i].Color : _color;
                newVerts[i] = new VertexPositionNormalColor(
                    cv.Position.ToXNAVector3(),
                    cv.Normal.ToXNAVector3(),
                    color);
            }

            try
            {
                ModelLock.EnterWriteLock();

                _workingVerts.Clear();
                _workingVerts.AddRange(newVerts);
                _workingEdges.Clear();
                _workingEdges.AddRange(newEdges);
                PublishUnlocked(force: true);
            }
            finally
            {
                ModelLock.ExitWriteLock();
            }

            CompositeManifoldReport = default;
            if (BajajMeshGenerator.VerboseLogging || MeshPhaseTimings.Enabled)
            {
                using (MeshPhaseTimings.Measure(MeshPhase.ManifoldValidate, composite.Faces.Count))
                    CompositeManifoldReport = MeshManifoldValidator.Validate(composite);

                int awayFromNonManifold = MeshWindingDiagnostics.CountInconsistentAwayFromNonManifold(composite);
                System.Diagnostics.Trace.WriteLine(
                    $"Composite winding: {result.BeforeInconsistent} -> {result.AfterInconsistent} inconsistent edges, " +
                    $"awayFromNonManifold={awayFromNonManifold} (after Reorient {result.AfterInconsistentAwayFromNonManifold}), " +
                    $"{result.TotalReversals} reversals, {outwardFlips} components flipped outward, {repairAfterOutward} repaired.  " +
                    $"Composite {CompositeManifoldReport}");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine(
                    $"Composite winding: {result.BeforeInconsistent} -> {result.AfterInconsistent} inconsistent edges, " +
                    $"{result.TotalReversals} reversals, {outwardFlips} components flipped outward, {repairAfterOutward} repaired.");
            }
        }

        /// <summary>
        /// Update working buffers from a merge or additional slice. Thread safe. Publishes at most ~25 Hz
        /// unless this is the first geometry for the model.
        /// </summary>
        private void UpdateModel(ICollection<VertexPositionNormalColor> modelVerts, int[] NewModelEdges, int[] mesh_to_global = null)
        {
            try
            {
                ModelLock.EnterWriteLock();

                if (modelVerts.Count > 0)
                    _workingVerts.AddRange(modelVerts);

                if (mesh_to_global is not null)
                {
                    for (int i = 0; i < mesh_to_global.Length; i++)
                    {
                        int iVert = mesh_to_global[i];
                        if ((uint)iVert >= (uint)_workingVerts.Count)
                            continue;

                        VertexPositionNormalColor v = _workingVerts[iVert];
                        v.Normal = composite[iVert].Normal.ToXNAVector3();
                        _workingVerts[iVert] = v;
                    }
                }

                if (NewModelEdges is { Length: > 0 })
                    _workingEdges.AddRange(NewModelEdges);

                Volatile.Write(ref _publishDirty, 1);
                PublishUnlocked(force: false);
            }
            finally
            {
                ModelLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Swap immutable arrays onto <see cref="model"/> for the draw thread. Caller must hold the write lock.
        /// </summary>
        void PublishUnlocked(bool force)
        {
            if (!force && Volatile.Read(ref _publishDirty) == 0)
                return;

            long now = Stopwatch.GetTimestamp();
            if (!force && _lastPublishTimestamp != 0 && (now - _lastPublishTimestamp) < PublishIntervalTicks)
                return;

            if (_workingVerts.Count == 0 || _workingEdges.Count == 0)
            {
                Volatile.Write(ref _publishDirty, 0);
                return;
            }

            VertexPositionNormalColor[] verts = [.. _workingVerts];
            int[] edges = [.. _workingEdges];
            model.Vertices = verts;
            model.Edges = edges;
            _lastPublishTimestamp = now;
            Volatile.Write(ref _publishDirty, 0);
        }

        /// <summary>
        /// Force a publish of the current working buffers (e.g. root finalize already did; exposed for callers
        /// that need the latest geometry before Draw).
        /// </summary>
        public void PublishNow()
        {
            try
            {
                ModelLock.EnterWriteLock();
                PublishUnlocked(force: true);
            }
            finally
            {
                ModelLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Mutate working vertex colors under the write lock and force-publish. Used for slice selection highlight.
        /// </summary>
        public bool EditWorkingVertexColors(Func<IReadOnlyList<VertexPositionNormalColor>, Color[]> edit)
        {
            try
            {
                ModelLock.EnterWriteLock();
                if (_workingVerts.Count == 0)
                    return false;

                Color[] next = edit(_workingVerts);
                if (next is null || next.Length != _workingVerts.Count)
                    return false;

                for (int i = 0; i < _workingVerts.Count; i++)
                {
                    VertexPositionNormalColor v = _workingVerts[i];
                    v.Color = next[i];
                    _workingVerts[i] = v;
                }

                PublishUnlocked(force: true);
                return true;
            }
            finally
            {
                ModelLock.ExitWriteLock();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="other"></param>
        public void Merge(SliceGraphMeshModel other)
        {
            Mesh3D<MorphMeshVertex> mesh = other.composite;

            using var _phase = MeshPhaseTimings.Measure(MeshPhase.MergeCombine, mesh.Vertices.Count);

            MergeAccumulatedSliceTopology(other);

            int[] mesh_to_global = new int[mesh.Vertices.Count];

            List<VertexPositionNormalColor> modelVerts = new(mesh.Vertices.Count);

            for (int iVert = 0; iVert < mesh.Vertices.Count; iVert++)
            {
                MorphMeshVertex vertex = mesh[iVert];
                MorphMeshVertex composite_vertex = MorphMeshVertex.Duplicate(vertex);

                if (vertex.ShapeIndex is null)
                {
                    int iNewVert = composite.AddVertex(composite_vertex);

                    modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color));

                    mesh_to_global[iVert] = iNewVert;
                }
                else
                {
                    if (false == ShapeIndexToVertex.TryGetValue(composite_vertex.ShapeIndex, out int iGlobalVert))
                    {
                        iGlobalVert = composite.AddVertex(composite_vertex);
                        ShapeIndexToVertex.Add(composite_vertex.ShapeIndex, iGlobalVert);

                        modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color));
                    }

                    mesh_to_global[iVert] = iGlobalVert;
                }
            }

            AddEdgesToComposite(mesh.Edges.Keys, mesh_to_global);

            int[] NewModelEdges = AddFacesToComposite(mesh.Faces, mesh_to_global);

            using (MeshPhaseTimings.Measure(MeshPhase.MergeNormals, composite.Vertices.Count))
                composite.RecalculateNormals(mesh_to_global);

            UpdateModel(modelVerts, NewModelEdges, mesh_to_global);
        }
    }
}
