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
    public static class GeometryArbitraries
    {
        public static void Register()
        {
            Arb.Register<GridVector2Generators>();
            Arb.Register<GridLineSegmentGenerators>();
            Arb.Register<TriangulatedMeshGenerators>();
            Arb.Register<ConstrainedDelaunayModelGenerators>();
        }

        public static Arbitrary<GridVector2> PointGenerator()
        {
            return GridVector2Generators.ArbRandomPoint();
        }

        public static Arbitrary<GridVector2[]> DistinctPointsGenerator()
        {
            return GridVector2Generators.ArbRandomDistinctPoints();
        }

        public static Arbitrary<GridLineSegment> LineSegmentGenerator()
        {
            return GridLineSegmentGenerators.ArbRandomLine();
        }

        public static Arbitrary<TriangulationMesh<IVertex2D>> TriangulatedMeshGenerator()
        {
            return TriangulatedMeshGenerators.ArbRandomMesh();
        }
    }

    public class GridLineSegmentGenerators
    {
        public static Arbitrary<GridLineSegment> ArbRandomLine()
        {
            return Arb.From(GenLine());  
        }

        public static Gen<GridLineSegment> GenLine()
        {
            return GridVector2Generators.Fresh()
                .Two()
                .Where(t => t.Item1 != t.Item2)
                .Select(t => new GridLineSegment(t.Item1, t.Item2));


            var coords = Arb.Default.NormalFloat().Generator.Four();
            return coords.Select(t => new GridLineSegment( new GridVector2((double)t.Item1, (double)t.Item2),
                                                           new GridVector2((double)t.Item3, (double)t.Item4)));
        } 
    }

    public class GridVector2Generators
    {
        static Gen<GridVector2> GridPoints = ChooseFrom(PointsOnGrid1D(201, 201, new GridRectangle(-100, 100, -100, 100)));

        public static Arbitrary<GridVector2> ArbRandomPoint()
        {
            return Arb.From(Fresh());
        }
        
        public static Arbitrary<GridVector2[]> ArbRandomDistinctPoints()
        {
            return Arb.From(GenDistinctPoints(), Arb.Default.Array<GridVector2>().Shrinker );
        }

        public static Gen<GridVector2> Fresh()
        {
            Gen<GridVector2> RandPoints = GenPoint();
            return Gen.Frequency(
                Tuple.Create(1, RandPoints),
                Tuple.Create(1, GridPoints));
        }

        private static GridVector2[] PointsOnGrid1D(int GridDimX, int GridDimY, GridRectangle bounds)
        {
            GridVector2[,] points = PointsOnGrid(GridDimX, GridDimY, bounds);
            List<GridVector2> listPoints = new List<GridVector2>(GridDimX * GridDimY);

            for(int i = 0; i < points.GetLength(0); i++)
            {
                for (int j = 0; j < points.GetLength(1); j++)
                {
                    listPoints.Add(points[i, j]);
                }
            }

            return listPoints.ToArray();
        }

        private static GridVector2[,] PointsOnGrid(int GridDimX, int GridDimY, GridRectangle bounds)
        {
            GridVector2[,] points = new GridVector2[GridDimX,GridDimY];
            double XStep = bounds.Width / (GridDimX-1);
            double YStep = bounds.Height / (GridDimY-1);

            double X = bounds.Left; 
            for (int iX = 0; iX < GridDimX; iX++)
            {
                double Y = bounds.Bottom;
                for(int iY = 0; iY < GridDimY; iY++)
                {
                    points[iX, iY] = new GridVector2(X, Y);
                    Y += YStep;
                }

                X += XStep;
            }

            return points;
        }
        public static Gen<GridVector2[]> GenDistinctPoints()
        {
            return Gen.Sized(size => GenDistinctPoints(size));
        }
        
        public static Gen<GridVector2[]> GenDistinctPoints(int nPoints)
        {
            return Fresh().ArrayOf(nPoints).Where(points => points.Distinct().Count() == nPoints);
        } 

        public static Gen<GridVector2> ChooseFrom(GridVector2[] items)
        {
            return from i in Gen.Choose(0, items.Length-1)
                   select items[i];
        }

        private static Gen<GridVector2> GenPoint()
        {
            var coords = Arb.Default.NormalFloat().Generator.Two();
            return coords.Select(t => new GridVector2((double)t.Item1, (double)t.Item2));
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
