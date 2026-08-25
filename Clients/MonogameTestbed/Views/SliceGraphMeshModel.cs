using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    /// <summary>
    /// Builds a single merged mesh from all of the completed slices of a slice graph. 
    /// Exposes a lock for using the model safely from a renderer.
    /// </summary>
    public class SliceGraphMeshModel : IColorView
    {
        //SliceGraph Graph;

        /// <summary>
        /// The composite mesh.  Not thread safe or protected by modeLock
        /// </summary>
        public Mesh3D<MorphMeshVertex> composite = new();

        /// <summary>
        /// A model of the final mesh.  Can be protected via modeLock for rendering the model as it is constructed
        /// </summary>
        public MeshModel<VertexPositionNormalColor> model = new();

        private readonly Dictionary<IShapeIndex, int> ShapeIndexToVertex = [];

        readonly List<MorphMeshOutwardOrientation.ShapeAtZ> _shapesAtZ = [];
        readonly Dictionary<int, bool> _isUpperByMorphShape = [];

        public ReaderWriterLockSlim ModelLock = new();

        /// <summary>
        /// The manifold state of the merged composite, measured after the winding pass.  A correct reconstruction
        /// is closed: every slice seam is shared by two faces once its neighbor has been merged in.
        /// </summary>
        public MeshManifoldReport CompositeManifoldReport { get; private set; }

        private Color _color = Color.CornflowerBlue;
        public Color Color
        {
            get => _color;
            set
            {
                if (value != _color)
                {
                    model.SetColor(value);
                    _color = value;
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

        /// <summary>
        /// </summary>
        /// <param name="mesh"></param>
        public void AddSlice(BajajGeneratorMesh mesh)
        {
            AccumulateSliceTopology(mesh.Topology);

            //Maps mesh vertex index to the global vertex index
            int[] mesh_to_global = new int[mesh.Vertices.Count];

            List<VertexPositionNormalColor> modelVerts = new(mesh.Vertices.Count);

            ///Add all new verticies to the mesh and populate a map for vertex indicies
            for (int iVert = 0; iVert < mesh.Vertices.Count; iVert++)
            {
                MorphMeshVertex vertex = mesh[iVert];

                if (vertex.ShapeIndex is null)
                {
                    //It is not part of a polygon, so we know the vertex will not collide with another vertex and need remapping
                    MorphMeshVertex composite_vertex = MorphMeshVertex.Duplicate(vertex);
                    int iNewVert = composite.AddVertex(composite_vertex);

                    modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color));

                    mesh_to_global[iVert] = iNewVert;
                }
                else
                {
                    //Check if the PointIndex for this vertex already exists in the model
                    ulong iShape = mesh.Topology.ShapeIndexToMorphNodeIndex[vertex.ShapeIndex.ShapeIndex];
                    MorphMeshVertex composite_vertex = MorphMeshVertex.Reindex(vertex, (int)iShape);

                    if (false == ShapeIndexToVertex.TryGetValue(composite_vertex.ShapeIndex, out int iGlobalVert))
                    {
                        //If the vertex is not in the mesh already, then add it.
                        iGlobalVert = composite.AddVertex(composite_vertex);
                        ShapeIndexToVertex.Add(composite_vertex.ShapeIndex, iGlobalVert);

                        modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color));
                    }

                    mesh_to_global[iVert] = iGlobalVert;
                }
            }

            //Translate edges and faces to the composite mesh
            AddEdgesToComposite(mesh.Edges.Keys, mesh_to_global);

            int[] NewModelEdges = AddFacesToComposite(mesh.Faces, mesh_to_global);

            //Update the normals for any vertex that was affected
            composite.RecalculateNormals(mesh_to_global);

            UpdateModel(modelVerts, NewModelEdges, mesh_to_global);

        }

        /// <summary>
        /// Adds edges to the composite mesh, mapping indicies using mesh_to_global
        /// </summary>
        /// <param name="edges"></param>
        /// <param name="mesh_to_global"></param>
        /// <returns></returns>
        private Geometry.Meshing.Edge[] AddEdgesToComposite(IEnumerable<IEdgeKey> edges, int[] mesh_to_global)
        {
            Edge[] newEdges = [.. edges.Select(k => new Edge(mesh_to_global[k.A], mesh_to_global[k.B]))];
            foreach (Edge composite_edge in newEdges)
            {
                composite.AddEdge(composite_edge);
            }

            return newEdges;
        }

        /// <summary>
        /// Adds faces to the composite mesh, mapping indicies using mesh_to_global
        /// </summary>
        /// <param name="faces"></param>
        /// <param name="mesh_to_global"></param>
        /// <returns></returns>
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
                //composite.AddFace(composite_face);
                composite_faces[iCompositeFace] = composite_face;

                Array.Copy(iMapped, 0, NewModelEdges, iModelFace, iMapped.Length);

                //Add the face to our model
                iModelFace += iMapped.Length;
                iCompositeFace += 1;
            }

            //Add the composite faces in one bulk move
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

        /// <summary>
        /// Merge another model's accumulated contour context into this one.  Used when compositing two
        /// SliceGraphMeshModels so the survivor retains the shapes needed for outward winding orientation.
        /// </summary>
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
            var options = new MeshWindingReorientation.Options
            {
                RespectAnchorFaces = false,
                AlwaysOrientOutward = false,
                RunRepairPass = false
            };

            var result = MeshWindingReorientation.Reorient(composite, options);

            var outwardCtx = MorphMeshOutwardOrientation.ShapeContext.FromAccumulated(_shapesAtZ, _isUpperByMorphShape);
            int outwardFlips = MorphMeshOutwardOrientation.OrientComponentsOutward(composite, outwardCtx);

            //Greedy repair walks every inconsistent pair and flips one face.  On a composite with
            //non-manifold junctions that pass does not converge: RC1 structure 1724 spent 98k reversals
            //and finished with *more* inconsistent edges than Reorient left.  Those reversed walls are
            //what backface culling turns into gaps in the tube.  Repair while the surface is manifold.
            int repairAfterOutward = 0;
            var afterOutward = MeshWindingDiagnostics.Analyze(composite);
            if (afterOutward.NonManifoldEdges == 0)
                repairAfterOutward = MeshWindingReorientation.RepairManifoldConsistency(composite);

            composite.RecalculateNormals();

            try
            {
                ModelLock.EnterWriteLock();

                //Triangle index order must match reoriented composite faces or backface culling ignores the fix.
                model.Edges = [.. composite.Faces.SelectMany(f => f.iVerts)];

                for (int i = 0; i < composite.Vertices.Count && i < model.Vertices.Length; i++)
                {
                    var v = model.Vertices[i];
                    v.Normal = composite[i].Normal.ToXNAVector3();
                    model.Vertices[i] = v;
                }

                //In-place vertex edits do not mark buffers dirty; reassign to force GPU refresh.
                model.Vertices = [.. model.Vertices];
            }
            finally
            {
                ModelLock.ExitWriteLock();
            }

            CompositeManifoldReport = MeshManifoldValidator.Validate(composite);
            int awayFromNonManifold = MeshWindingDiagnostics.CountInconsistentAwayFromNonManifold(composite);
            System.Diagnostics.Trace.WriteLine(
                $"Composite winding: {result.BeforeInconsistent} -> {result.AfterInconsistent} inconsistent edges, " +
                $"awayFromNonManifold={awayFromNonManifold} (after Reorient {result.AfterInconsistentAwayFromNonManifold}), " +
                $"{result.TotalReversals} reversals, {outwardFlips} components flipped outward, {repairAfterOutward} repaired.  " +
                $"Composite {CompositeManifoldReport}");
        }

        /// <summary>
        /// Update our mesh model with new verticies and edges from a merge or additional slice operation.  Thread safe.
        /// </summary>
        /// <param name="verts">Vertices to append to our model</param>
        /// <param name="edges">Triangles to add to the model, expects sets of three indicating triangles.</param>
        /// <param name="mesh_to_global">The indicies of vertices whose normal needs to be updated using the composite mesh normal</param>
        private void UpdateModel(ICollection<VertexPositionNormalColor> modelVerts, int[] NewModelEdges, int[] mesh_to_global = null)
        {
            try
            {
                ModelLock.EnterWriteLock();

                //Add all new verticies to our model
                model.AppendVerticies(modelVerts);
                model.AppendEdges(NewModelEdges); //Add all new edges to our model

                if (mesh_to_global is null)
                    return;

                //Update the normals for our model
                for (int i = 0; i < mesh_to_global.Length; i++)
                {
                    int iVert = mesh_to_global[i];

                    model.Vertices[iVert].Normal = composite[iVert].Normal.ToXNAVector3();
                }
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
            // When we merge another SliceGraphMeshModel we know the PolyIndex values for the other model match our own.  We need to create new verticies, edges, and faces into our models
            Mesh3D<MorphMeshVertex> mesh = other.composite;

            //Carry over the other model's accumulated contour context.  The binary-tree assembly merges child
            //models into a single survivor; without this the root's EnsureCompositeWinding would only see the
            //shapes from one subtree and orient the rest of the surface using incomplete context.
            MergeAccumulatedSliceTopology(other);

            //Note: verticies with a null ShapeIndex (medial-axis / cap verticies) are intentionally not merged
            //across slices.  Each slice places its medial-axis verticies at that slice's center Z and caps only
            //exist on open ends, so these interior points never coincide between slices.  Merging them by
            //position would risk welding distinct points and pinching the surface.

            //Maps mesh vertex index to the global vertex index
            int[] mesh_to_global = new int[mesh.Vertices.Count];

            List<VertexPositionNormalColor> modelVerts = new(mesh.Vertices.Count);

            ///Add all new verticies to the mesh and populate a map for vertex indicies
            for (int iVert = 0; iVert < mesh.Vertices.Count; iVert++)
            {
                MorphMeshVertex vertex = mesh[iVert];
                MorphMeshVertex composite_vertex = MorphMeshVertex.Duplicate(vertex);

                if (vertex.ShapeIndex is null)
                {
                    //It is not part of a polygon, so we know the vertex will not collide with another vertex and need remapping

                    int iNewVert = composite.AddVertex(composite_vertex);

                    modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color));

                    mesh_to_global[iVert] = iNewVert;
                }
                else
                {
                    // When we merge another SliceGraphMeshModel we know the PolyIndex values for the other model match our own.  We need to create new verticies, edges, and faces into our models

                    //Check if the PointIndex for this vertex already exists in the model 
                    if (false == ShapeIndexToVertex.TryGetValue(composite_vertex.ShapeIndex, out int iGlobalVert))
                    {
                        //If the vertex is not in the mesh already, then add it.
                        iGlobalVert = composite.AddVertex(composite_vertex);
                        ShapeIndexToVertex.Add(composite_vertex.ShapeIndex, iGlobalVert);

                        modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color));
                    }

                    mesh_to_global[iVert] = iGlobalVert;
                }
            }

            //Translate edges and faces to the composite mesh
            AddEdgesToComposite(mesh.Edges.Keys, mesh_to_global);

            int[] NewModelEdges = AddFacesToComposite(mesh.Faces, mesh_to_global);

            //Update the normals for any vertex that was affected
            composite.RecalculateNormals(mesh_to_global);

            UpdateModel(modelVerts, NewModelEdges, mesh_to_global);
        }


    }
}
