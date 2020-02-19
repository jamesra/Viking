using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FsCheck;
using Geometry;
using Geometry.Meshing;
using GeometryTests.Algorithms;

namespace GeometryTests
{
    public static class GeometryMeshingArbitraries
    {
        public static void Register()
        {
            Arb.Register<TriangulatedMeshGenerators>();
            Arb.Register<ConstrainedDelaunayModelGenerators>();
        }

        public static Arbitrary<TriangulationMesh<IVertex2D>> TriangulatedMeshGenerator()
        {
            return TriangulatedMeshGenerators.ArbRandomMesh();
        }
    }

    
    public class TriangulatedMeshGenerators
    {
        /// <summary>
        /// Function to report incremental mesh generation progress to
        /// </summary>
        public static TriangulationMesh<IVertex2D>.ProgressUpdate OnProgress = null;

        public static Gen<TriangulationMesh<IVertex2D>> GenMesh(int nVerts)
        {
            return GridVector2Generators.Fresh().ArrayOf(nVerts)
                .Where(points => points.Distinct().Count() == points.Length)
                .Select(verts => GenericDelaunayMeshGenerator2D<IVertex2D>
                .TriangulateToMesh(verts.Select(v => new TriangulationVertex(v)).ToArray(), OnProgress));            
            //return GridVector2Generators.GenPoints().Select(verts => GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(verts.Select(v => new Vertex2D(v)).ToArray()));
        } 

        public static Gen<TriangulationMesh<IVertex2D>> RandomMesh()
        {
            return Gen.Sized(size => GenMesh(size)); 
        }

        public static Arbitrary<TriangulationMesh<IVertex2D>> ArbRandomMesh()
        {
            return Arb.From(RandomMesh(), MeshShrinker);
        }

        /// <summary>
        /// Shrink a mesh by randomly removing points.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="iVert">The vertex the shrinker removed</param>
        /// <returns></returns>
        public static IEnumerable<TriangulationMesh<IVertex2D>> MeshShrinker(TriangulationMesh<IVertex2D> mesh)
        {
            IVertex2D[] verts = mesh.Verticies.ToArray();

            for(int i = mesh.Verticies.Count-1; i >= 0; i--)
            {
                /*
                Vertex2D[] fewer_verts = new Vertex2D[mesh.Verticies.Count - 1];
                Array.Copy(verts, fewer_verts, i);
                Array.Copy(verts, i+1, fewer_verts, i, fewer_verts.Length - i);

                Vertex2D[] vert_clones = fewer_verts.Select(v => new Vertex2D(v.Position)).ToArray();

                TriangulationMesh<Vertex2D> newMesh = GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(vert_clones);
                yield return newMesh;
                */
                yield return RemoveMeshVert(mesh, i);
            }
        }

        /// <summary>
        /// Returns a copy of the mesh with the vertex at i removed
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="iVert">The vertex the shrinker removed</param>
        /// <returns></returns>
        public static TriangulationMesh<IVertex2D> RemoveMeshVert(TriangulationMesh<IVertex2D> mesh, int i)
        {
            IVertex2D[] verts = mesh.Verticies.ToArray();
             
            Vertex2D[] fewer_verts = new Vertex2D[mesh.Verticies.Count - 1];
            Array.Copy(verts, fewer_verts, i);
            Array.Copy(verts, i + 1, fewer_verts, i, fewer_verts.Length - i);

            IVertex2D[] vert_clones = fewer_verts.Select(v => v.ShallowCopy() as IVertex2D).ToArray();

            TriangulationMesh<IVertex2D> newMesh = GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh(vert_clones, OnProgress);
            return newMesh;
        }
    }

    /// <summary>
    /// Generates random points and a set of edges to serve as constraints
    /// Includes simplification code to ensure if verticies are removed the edges are still consistent
    /// </summary>
    public class ConstrainedDelaunayModelGenerators
    {

        public static Arbitrary<ConstrainedDelaunayModel> ArbRandomModel()
        {
            return Arb.From(Fresh(), ModelShrinker);
        }

        public static Gen<ConstrainedDelaunayModel> Fresh()
        {
            return Gen.Sized(size => GenModel(size));
        }

        public static Gen<ConstrainedDelaunayModel> GenModel(int size)
        {
            int nVerts = size;

            return TriangulatedMeshGenerators.GenMesh(nVerts).Select(mesh => new ConstrainedDelaunayModel(mesh));
        }

        /// <summary>
        /// Then by removing constraints.  Then by removing verticies.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="iVert">The vertex the shrinker removed</param>
        /// <returns></returns>
        public static IEnumerable<ConstrainedDelaunayModel> ModelShrinker(ConstrainedDelaunayModel model)
        {
            //Attempt to shrink the edge constraints
            for (int i = model.ConstraintEdges.Count - 1; i >= 0; i--)
            {
                yield return RemoveModelEdgeConstraint(model, i);
            }

            //Attempt to shrink the mesh
            for (int i = model.mesh.Verticies.Count - 1; i >= 0; i--)
            {
                yield return RemoveModelMeshVert(model, i);
            } 
        }

        private static ConstrainedDelaunayModel RemoveModelMeshVert(ConstrainedDelaunayModel model, int iVert)
        {
            TriangulationMesh<IVertex2D> newMesh = TriangulatedMeshGenerators.RemoveMeshVert(model.mesh, iVert);

            //OK, adjust all of our edge keys.  If the edge contained the vertex remove it.  If the edge is to
            //a vertex with a higher index decrement it.  This should result in edges connecting the same verticies
            //if they remain in the new mesh.

            List<EdgeKey> new_edges = new List<EdgeKey>(model.ConstraintEdges.Count);
            for(int i = 0; i < model.ConstraintEdges.Count; i++)
            {
                EdgeKey key = model.ConstraintEdges[i];
                
                if(key.A == iVert || key.B == iVert)
                {
                    //Edge vert was removed from mesh, do not include the edge
                    continue;
                }

                //Adjust Edge vert if needed
                EdgeKey new_key = new EdgeKey(key.A > iVert ? key.A - 1 : key.A,
                                                key.B > iVert ? key.B - 1 : key.B);
                new_edges.Add(new_key);
            }

            return new ConstrainedDelaunayModel(newMesh, new_edges);

        }

        private static ConstrainedDelaunayModel RemoveModelEdgeConstraint(ConstrainedDelaunayModel model, int iEdge)
        {
            var newMesh = model.mesh.Clone();

            //TriangulationMesh<IVertex2D> newMesh = GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh(vert_clones, TriangulatedMeshGenerators.OnProgress);

            List<EdgeKey> edge_clones = model.ConstraintEdges.ToList();
            edge_clones.RemoveAt(iEdge);
            ConstrainedDelaunayModel shrunk_model = new ConstrainedDelaunayModel(newMesh, edge_clones);
            
            return shrunk_model;
        }


        /*
        public static Gen<TriangulationMesh<Vertex2D>> GenMesh(int nVerts)
        {
            return GridVector2Generators.Fresh().ArrayOf(nVerts)
                .Where(points => points.Distinct().Count() == points.Length)
                .Select(verts => GenericDelaunayMeshGenerator2D<Vertex2D>
                .TriangulateToMesh(verts.Select(v => new Vertex2D(v)).ToArray()));
            //return GridVector2Generators.GenPoints().Select(verts => GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(verts.Select(v => new Vertex2D(v)).ToArray()));
        }

        public static Gen<TriangulationMesh<Vertex2D>> RandomMesh()
        {
            return Gen.Sized(size => GenMesh(size));
        }

        public static Arbitrary<TriangulationMesh<Vertex2D>> ArbRandomMesh()
        {
            return Arb.From(RandomMesh(), MeshShrinker);
        }

        /// <summary>
        /// Shrink a mesh by randomly removing points
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static IEnumerable<TriangulationMesh<Vertex2D>> MeshShrinker(TriangulationMesh<Vertex2D> mesh)
        {
            Vertex2D[] verts = mesh.Verticies.ToArray();

            for (int i = mesh.Verticies.Count - 1; i >= 0; i--)
            {
                Vertex2D[] fewer_verts = new Vertex2D[mesh.Verticies.Count - 1];
                Array.Copy(verts, fewer_verts, i);
                Array.Copy(verts, i + 1, fewer_verts, i, fewer_verts.Length - i);

                Vertex2D[] vert_clones = fewer_verts.Select(v => new Vertex2D(v.Position)).ToArray();

                TriangulationMesh<Vertex2D> newMesh = GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(vert_clones);
                yield return newMesh;
            }
        }
        */
    }



}
