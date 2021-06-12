using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VikingXNAGraphics;

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
        public Mesh3D<MorphMeshVertex> composite = new Mesh3D<MorphMeshVertex>();

        /// <summary>
        /// A model of the final mesh.  Can be protected via modeLock for rendering the model as it is constructed
        /// </summary>
        public MeshModel<VertexPositionNormalColor> model = new MeshModel<VertexPositionNormalColor>();

        private Dictionary<PolygonIndex, int> PolyIndexToVertex = new Dictionary<PolygonIndex, int>();

        public ReaderWriterLockSlim ModelLock = new ReaderWriterLockSlim();

        private Color _color = Color.CornflowerBlue;
        public Color Color { get { return _color; }
            set
            {
                if(value != _color)
                {
                    model.SetColor(value);
                    _color = value; 
                }
            }
        } 
        public float Alpha { get { return Color.GetAlpha(); } set { Color = Color.SetAlpha(value); } }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="position">Where in world space we want the model displayed</param>
        public SliceGraphMeshModel(GridVector3 position)
        {
            model.Position = position;
        } 

        /// <summary>
        /// </summary>
        /// <param name="mesh"></param>
        public void AddSlice(BajajGeneratorMesh mesh)
        {
            //Maps mesh vertex index to the global vertex index
            int[] mesh_to_global = new int[mesh.Verticies.Count];

            List<VertexPositionNormalColor> modelVerts = new List<VertexPositionNormalColor>(mesh.Verticies.Count);

            ///Add all new verticies to the mesh and populate a map for vertex indicies
            for (int iVert = 0; iVert < mesh.Verticies.Count; iVert++)
            {
                MorphMeshVertex vertex = mesh[iVert];
                
                if(vertex.PolyIndex.HasValue == false)
                {
                    //It is not part of a polygon, so we know the vertex will not collide with another vertex and need remapping
                    var composite_vertex = MorphMeshVertex.Duplicate(vertex);
                    int iNewVert = composite.AddVertex(composite_vertex);

                    modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color));

                    mesh_to_global[iVert] = iNewVert;
                }
                else
                {
                    //Check if the PointIndex for this vertex already exists in the model
                    ulong iPoly = mesh.Topology.PolyIndexToMorphNodeIndex[vertex.PolyIndex.Value.iPoly];
                    var composite_vertex = MorphMeshVertex.Reindex(vertex, (int)iPoly);

                    bool vertFound = PolyIndexToVertex.TryGetValue(composite_vertex.PolyIndex.Value, out int iGlobalVert);
                    if(vertFound == false)
                    {   
                        //If the vertex is not in the mesh already, then add it.
                        iGlobalVert = composite.AddVertex(composite_vertex);
                        PolyIndexToVertex.Add(composite_vertex.PolyIndex.Value, iGlobalVert);

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
        private IEnumerable<Edge> AddEdgesToComposite(IEnumerable<IEdgeKey> edges, int[] mesh_to_global)
        {
            Edge[] newEdges = edges.Select(k => new Edge(mesh_to_global[k.A], mesh_to_global[k.B])).ToArray();
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
            foreach (Face f in faces)
            {

                int[] iMapped = new int[f.iVerts.Length];
                for (int i = 0; i < f.iVerts.Length; i++)
                    iMapped[i] = mesh_to_global[f.iVerts[i]];

                Face composite_face = new Face(iMapped);
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

        /// <summary>
        /// Update our mesh model with new verticies and edges from a merge or additional slice operation.  Thread safe.
        /// </summary>
        /// <param name="verts">Verticies to append to our model</param>
        /// <param name="edges">Triangles to add to the model, expects sets of three indicating triangles.</param>
        /// <param name="mesh_to_global">The indicies of vertices whose normal needs to be updated using the composite mesh normal</param>
        private void UpdateModel(ICollection<VertexPositionNormalColor> modelVerts, int[] NewModelEdges, int[] mesh_to_global=null)
        {
            try
            {
                ModelLock.EnterWriteLock();

                //Add all new verticies to our model
                model.AppendVerticies(modelVerts);
                model.AppendEdges(NewModelEdges); //Add all new edges to our model

                if (mesh_to_global == null)
                    return;

                //Update the normals for our model
                for (int i = 0; i < mesh_to_global.Length; i++)
                {
                    int iVert = mesh_to_global[i];

                    model.Verticies[iVert].Normal = composite[iVert].Normal.ToXNAVector3();
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

            //Maps mesh vertex index to the global vertex index
            int[] mesh_to_global = new int[mesh.Verticies.Count];

            List<VertexPositionNormalColor> modelVerts = new List<VertexPositionNormalColor>(mesh.Verticies.Count);
            
            ///Add all new verticies to the mesh and populate a map for vertex indicies
            for (int iVert = 0; iVert < mesh.Verticies.Count; iVert++)
            {
                MorphMeshVertex vertex = mesh[iVert];
                var composite_vertex = MorphMeshVertex.Duplicate(vertex);

                if (vertex.PolyIndex.HasValue == false)
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
                    bool vertFound = PolyIndexToVertex.TryGetValue(composite_vertex.PolyIndex.Value, out int iGlobalVert);
                    if (vertFound == false)
                    {
                        //If the vertex is not in the mesh already, then add it.
                        iGlobalVert = composite.AddVertex(composite_vertex);
                        PolyIndexToVertex.Add(composite_vertex.PolyIndex.Value, iGlobalVert);

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
