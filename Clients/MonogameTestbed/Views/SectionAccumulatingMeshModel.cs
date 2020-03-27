using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; 
using VikingXNA; 
using VikingXNAGraphics;
using MorphologyMesh;
using System.Threading;

namespace MonogameTestbed
{
    /// <summary>
    /// Builds a single merged mesh from all of the completed slices of a slice graph. 
    /// Exposes a lock for using the model safely from a renderer.
    /// </summary>
    class SliceGraphMeshModel
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

        private Dictionary<PointIndex, int> PolyIndexToVertex = new Dictionary<PointIndex, int>();

        public ReaderWriterLockSlim ModelLock = new ReaderWriterLockSlim();

        public SliceGraphMeshModel()
        {
        }
        /*
        public SliceGraphMeshModel(SliceGraph graph)
        {
            Graph = graph;
        }*/

        public void AddSlice(BajajGeneratorMesh mesh)
        {
            //VertexPositionColor[] verts = mesh.Verticies.Select(v => new VertexPositionColor(v.Position.ToXNAVector3(), Color.Orange)).ToArray();

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

                    modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero, Color.CornflowerBlue));

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

                        modelVerts.Add(new VertexPositionNormalColor(composite_vertex.Position.ToXNAVector3(), Vector3.Zero,  Color.CornflowerBlue));
                    }

                    mesh_to_global[iVert] = iGlobalVert;
                }
            }

            
            //Translate edges and faces to the composite mesh
            foreach (EdgeKey e in mesh.Edges.Keys)
            {
                int iA = mesh_to_global[e.A];
                int iB = mesh_to_global[e.B];

                Edge composite_edge = new Edge(iA, iB);
                composite.AddEdge(composite_edge);
            }

           
            int[] NewModelEdges = new int[mesh.Faces.Count * 3];
            int iModelFace = 0;
            foreach (Face f in mesh.Faces)
            {
                int[] iMapped = new int[f.iVerts.Length];
                for (int i = 0; i < f.iVerts.Length; i++)
                    iMapped[i] = mesh_to_global[f.iVerts[i]];

                Face composite_face = new Face(iMapped);
                composite.AddFace(composite_face);
                     
                Array.Copy(iMapped, 0, NewModelEdges, iModelFace, iMapped.Length);

                //Add the face to our model
                iModelFace += iMapped.Length;
            }

            

            //Update the normals for any vertex that was affected
            composite.RecalculateNormals(mesh_to_global);

            try
            {
                ModelLock.EnterWriteLock();

                //Add all new verticies to our model
                model.AppendVerticies(modelVerts);
                model.AppendEdges(NewModelEdges); //Add all new edges to our model

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

        
    }
}
