using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Geometry.Meshing;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTree;

namespace GeometryTests.Algorithms
{
    public static class FSCheckMeshExtensions
    {
        /// <summary>
        /// A helper function to add .Classify calls to a property according to size
        /// </summary>
        /// <param name="prop"></param>
        /// <param name="nVerts"></param>
        /// <returns></returns>
        public static Property ClassifyMeshSize(this Property prop, int nVerts)
        {

            return prop.Classify(nVerts <= 3, "Trivial")
               .Classify(nVerts > 3 && nVerts <= 10, "3 - 10 Verts")
               .Classify(nVerts > 10 && nVerts <= 32, "11 - 32 Verts")
               .Classify(nVerts > 32 && nVerts <= 64, "33 - 64 Verts")
               .Classify(nVerts > 64 && nVerts <= 128, "65 - 128 Verts")
               .Classify(nVerts > 128 && nVerts <= 256, "129 - 256 Verts")
               .Classify(nVerts > 256 && nVerts <= 512, "257 - 512 Verts")
               .Classify(nVerts > 512 && nVerts <= 1024, "513 - 1024 Verts")
               .Classify(nVerts > 1024, "1024+ Verts");
        }

        /// <summary>
        /// A helper function to add .Classify calls to a property according to size
        /// </summary>
        /// <param name="prop"></param>
        /// <param name="nVerts"></param>
        /// <returns></returns>
        public static Property ClassifySize(this Property prop, int nVerts)
        {

            return prop.Classify(nVerts == 0, "0")
               .Classify(nVerts > 0 && nVerts <= 3, "1-3")
               .Classify(nVerts > 3 && nVerts <= 10, "3 - 10")
               .Classify(nVerts > 10 && nVerts <= 32, "11 - 32")
               .Classify(nVerts > 32 && nVerts <= 64, "33 - 64")
               .Classify(nVerts > 64 && nVerts <= 128, "65 - 128")
               .Classify(nVerts > 128 && nVerts <= 256, "129 - 256")
               .Classify(nVerts > 256 && nVerts <= 512, "257 - 512")
               .Classify(nVerts > 512 && nVerts <= 1024, "513 - 1024")
               .Classify(nVerts > 1024, "1024+");
        }
    }

    [TestClass]
    public class DelaunayTest
    {
        /*
        [TestMethod]
        public void DelaunayParameterTest()
        {
            Arb.Register<GridVector2Generators>();

            Func<GridVector2[], bool> SplitFunc = GenAndTriangulateMesh;

            Prop.ForAll<GridVector2[]>((points) => SplitFunc(points)
                .When(points.Distinct().Count() >= 3)
                .Classify(AllPointsColinear(points), "Colinear"))
                .QuickCheckThrowOnFailure();
            
        }
        */
        
        [TestMethod]
        public void DelaunayGeneratorParameterTest()
        {
            //A second pass implementation that generates entire meshes as random parameters and not sets of points that I must convert to meshes
            GeometryArbitraries.Register();
            GeometryMeshingArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 1000;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 0;
            
            //Prop.ForAll<TriangulationMesh<Vertex2D>>((mesh) => IsDelaunay(mesh)).Check(configuration);
            Prop.ForAll<GridVector2[]>((points) => IsDelaunay(GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh(points.Select(p => new Vertex2D(p)).ToArray()))).Check(configuration);

            //    .When(points.Distinct().Count() >= 3)
              //  .Classify(AllPointsColinear(points), "Colinear"))
                //.QuickCheckThrowOnFailure();

            /*
            Prop.ForAll<GridVector2[]>((points) =>
            GenAndTriangulateMesh(points, out TriangulationMesh<Vertex2D> mesh)
            
                .And(() => IsDelaunay(mesh))
                .When(points.Distinct().Count() >= 3)
                .Classify((points) => AllPointsColinear(points), "Colinear")
                .QuickCheckThrowOnFailure();
                */
        }

        [TestMethod]
        public void DelaunayGeneratorParameterTest2()
        {
            //A second pass implementation that generates entire meshes as random parameters and not sets of points that I must convert to meshes
            GeometryArbitraries.Register();
            GeometryMeshingArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 1000;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 0;

            //Prop.ForAll<TriangulationMesh<Vertex2D>>((mesh) => IsDelaunay(mesh)).Check(configuration);
            Prop.ForAll<TriangulationMesh<IVertex2D>>((mesh) => IsDelaunay(mesh)).Check(configuration);

            //    .When(points.Distinct().Count() >= 3)
            //  .Classify(AllPointsColinear(points), "Colinear"))
            //.QuickCheckThrowOnFailure();

            /*
            Prop.ForAll<GridVector2[]>((points) =>
            GenAndTriangulateMesh(points, out TriangulationMesh<Vertex2D> mesh)
            
                .And(() => IsDelaunay(mesh))
                .When(points.Distinct().Count() >= 3)
                .Classify((points) => AllPointsColinear(points), "Colinear")
                .QuickCheckThrowOnFailure();
                */
        }

        private bool IsValidConstrainedDelaunayInput(GridVector2[] points, int[] edges)
        {
            int nPoints = points.Distinct().Count();
            if (nPoints < 3)
            {
                return false;
            }

            if (edges.Length > 0)
            {
                if (edges.Max() >= nPoints)
                {
                    return false;
                }

                if (edges.Min() < 0)
                {
                    return false;
                }

                if (edges.Length < 2)
                {
                    return false;
                }
            }

            return true;
        }
        /*
        [TestMethod]
        public void ConstrainedDelaunayParameterTest()
        {
            GeometryArbitraries.Register();

            Func<TriangulationMesh<Vertex2D>, int[], bool> SplitFunc = GenTriangulateAndConstrainMesh;
            /*
            Prop.ForAll<GridVector2[],int[]>((mesh, edges) => SplitFunc(mesh, edges.Distinct().ToArray())
                .When( IsValidConstrainedDelaunayInput(points, edges))
                .Classify(AllPointsColinear(points), "Colinear"))
                .Check(new Configuration { Replay = FsCheck.Random.StdGen.NewStdGen(1279530810, 296702734), Runner = Config.QuickThrowOnFailure.Runner });
            *//*
        }
    */
        [TestMethod]
        public void ConstrainedDelaunayParameterTest()
        {
            GeometryArbitraries.Register();
            GeometryMeshingArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest =  100;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 1;
            

            //Func<int, int, ConstrainedDelaunaySpec> func = (nVerts, nEdges) => new ConstrainedDelaunaySpec(nVerts, nEdges);
            Prop.ForAll<GridVector2[], ushort[]>((points, edges) =>
            {
                var mesh = GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(points.Select(v => new Vertex2D(v, null)).ToArray());
                var edge_configuration = Configuration.QuickThrowOnFailure;
                edge_configuration.MaxNbOfTest = 2;
                edge_configuration.QuietOnSuccess = true;
                edge_configuration.StartSize = edges.Length-1;


                new ConstrainedDelaunaySpec(points, edges.Select(e => (int)e).Distinct().ToArray()).ToProperty().Check(edge_configuration);
            }).Check(configuration);
            
            //Prop.ForAll<ushort, ushort>((nVerts, nEdges) => new ConstrainedDelaunaySpec(nVerts, nEdges).ToProperty().QuickCheckThrowOnFailure()).QuickCheckThrowOnFailure();
            //Prop.ForAll<ushort>((nVerts) => new ConstrainedDelaunaySpec(nVerts, nVerts / 2).ToProperty().QuickCheck()).Check(configuration);
            //Prop.ForAll<TriangulationMesh<Vertex2D>>((mesh) => new ConstrainedDelaunaySpec(mesh).ToProperty().QuickCheck()).Check(configuration);
            //Prop.ForAll<ushort>((nVerts) => new ConstrainedDelaunaySpec(nVerts, nVerts / 2).ToProperty().QuickCheckThrowOnFailure()).QuickCheckThrowOnFailure();
            //Prop.ForAll<ushort, ushort>((nVerts, nEdges) => new ConstrainedDelaunaySpec(nVerts < nEdges ? nEdges : nVerts, nEdges < nVerts ? nEdges : nVerts)).QuickCheck();
            //Prop.ForAll<ushort, ushort>((nVerts,nEdges) => ).QuickCheck();
            /*
            Prop.ForAll<GridVector2[],int[]>((mesh, edges) => SplitFunc(mesh, edges.Distinct().ToArray())
                .When( IsValidConstrainedDelaunayInput(points, edges))
                .Classify(AllPointsColinear(points), "Colinear"))
                .Check(new Configuration { Replay = FsCheck.Random.StdGen.NewStdGen(1279530810, 296702734), Runner = Config.QuickThrowOnFailure.Runner });
            */
            //new ConstrainedDelaunaySpec(16, 16 / 2).ToProperty().Check(new Configuration { Replay = FsCheck.Random.StdGen.NewStdGen(1616214556, 296703506), Runner = Config.QuickThrowOnFailure.Runner });

        }

        [TestMethod]
        public void ConstrainedDelaunayTestWithArbModel()
        {
            GeometryArbitraries.Register();
            GeometryMeshingArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 500;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 1;


            //Func<int, int, ConstrainedDelaunaySpec> func = (nVerts, nEdges) => new ConstrainedDelaunaySpec(nVerts, nEdges);

            Prop.ForAll<ConstrainedDelaunayModel>((model) =>
            {
                var model_configuration = Configuration.QuickThrowOnFailure;
                model_configuration.MaxNbOfTest = 1;
                model_configuration.QuietOnSuccess = true;
                model_configuration.StartSize = model.ConstraintEdges.Count/2; //Set the number of edges so the correct number of Commands are generated

                var spec = new ConstrainedDelaunaySpec(model);
                var spec_prop = spec.ToProperty();
                //spec_prop.Check(model_configuration);
                return spec_prop;
                
            }).Check(configuration);
            
            /*
            Prop.ForAll<ConstrainedDelaunayModel>((model) =>
            {
                var model_configuration = Configuration.VerboseThrowOnFailure;
                model_configuration.MaxNbOfTest = 1;
                model_configuration.QuietOnSuccess = false;
                model_configuration.StartSize = model.ConstraintEdges.Count / 2; //Set the number of edges so the correct number of Commands are generated

                new ConstrainedDelaunaySpec(model).ToProperty().Check(model_configuration);
            }).Check(configuration);
            */
            //Prop.ForAll<ushort, ushort>((nVerts, nEdges) => new ConstrainedDelaunaySpec(nVerts, nEdges).ToProperty().QuickCheckThrowOnFailure()).QuickCheckThrowOnFailure();
            //Prop.ForAll<ushort>((nVerts) => new ConstrainedDelaunaySpec(nVerts, nVerts / 2).ToProperty().QuickCheck()).Check(configuration);
            //Prop.ForAll<TriangulationMesh<Vertex2D>>((mesh) => new ConstrainedDelaunaySpec(mesh).ToProperty().QuickCheck()).Check(configuration);
            //Prop.ForAll<ushort>((nVerts) => new ConstrainedDelaunaySpec(nVerts, nVerts / 2).ToProperty().QuickCheckThrowOnFailure()).QuickCheckThrowOnFailure();
            //Prop.ForAll<ushort, ushort>((nVerts, nEdges) => new ConstrainedDelaunaySpec(nVerts < nEdges ? nEdges : nVerts, nEdges < nVerts ? nEdges : nVerts)).QuickCheck();
            //Prop.ForAll<ushort, ushort>((nVerts,nEdges) => ).QuickCheck();
            /*
            Prop.ForAll<GridVector2[],int[]>((mesh, edges) => SplitFunc(mesh, edges.Distinct().ToArray())
                .When( IsValidConstrainedDelaunayInput(points, edges))
                .Classify(AllPointsColinear(points), "Colinear"))
                .Check(new Configuration { Replay = FsCheck.Random.StdGen.NewStdGen(1279530810, 296702734), Runner = Config.QuickThrowOnFailure.Runner });
            */
            //new ConstrainedDelaunaySpec(16, 16 / 2).ToProperty().Check(new Configuration { Replay = FsCheck.Random.StdGen.NewStdGen(1616214556, 296703506), Runner = Config.QuickThrowOnFailure.Runner });

        }

        static public bool AllPointsColinear(GridVector2[] points)
        {
            points = points.Distinct().ToArray();

            if (points.Length < 3)
            {
                return true;
            }

            //Check if points are all colinear
            {
                GridLineSegment baseline = new GridLineSegment(points[0], points[1]);

                int i;
                for (i = 2; i < points.Length; i++)
                {
                    if (baseline.IsLeft(points[i]) != 0)
                    {
                        return false;
                    }
                }

                //OK, we checked all of the points and did not exit the loop early, the points must be colinear.
                if (i == points.Length)
                {
                    return true;
                }
            }

            return false;
        }
         

        public Property IsDelaunay(TriangulationMesh<IVertex2D> mesh)
        {
            //System.Diagnostics.Trace.WriteLine(string.Format("{0}", mesh));
            bool edgesIntersect = mesh.AnyMeshEdgesIntersect();
            bool facesDelaunay = AreTriangulatedFacesDelaunay(mesh);
            bool facesCCW = AreTriangulatedFacesCCW(mesh);
            bool facesColinear = AreTriangulatedFacesColinear(mesh);
            bool vertEdges = AreTriangulatedVertexEdgesValid(mesh) || mesh.Verticies.Count < 3;
            bool facesAreTriangles = DelaunayTest.AreFacesTriangles(mesh);
            bool success = (edgesIntersect == false) && facesDelaunay && facesCCW && vertEdges && facesAreTriangles;
            int nVerts = mesh.Verticies.Count;
            return (edgesIntersect == false).Label("Edges intersect")
               .And(facesDelaunay.Label("Faces not Delaunay"))
               .And(facesCCW.Label("Faces Clockwise"))
               .And((facesColinear == false).Label("Faces colinear"))
               .And(facesAreTriangles.Label("Faces aren't triangles"))
               .And(vertEdges.Label("Verts with 0 or 1 edges"))
               .ClassifyMeshSize(nVerts)
               .Label(mesh.ToJSON());
        }

        

        public bool GenAndTriangulateMesh(GridVector2[] points)
        {
            return GenAndTriangulateMesh(points, out TriangulationMesh<IVertex2D> mesh);
        }

        public static bool GenAndTriangulateMesh(GridVector2[] points, out TriangulationMesh<IVertex2D> mesh)
        {
            mesh = null;
            points = points.Distinct().ToArray();

            if (AllPointsColinear(points))
            {
                
                return true;
            }

            try
            {
                Vertex2D[] InputVerts = points.Select(p => new Vertex2D(p)).ToArray();
                mesh = GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh(InputVerts, null);
                
                if (AreTriangulatedVertexEdgesValid(mesh) == false)
                    return false;

                if (mesh.AnyMeshEdgesIntersect())
                    return false;

                if (AreTriangulatedFacesCCW(mesh) == false)
                    return false;

                if (AreTriangulatedFacesDelaunay(mesh) == false)
                    return false;

                return true;
            }
            catch (ArgumentException e)
            {
                return false;
            }
        }
        /*
        public bool GenTriangulateAndConstrainMesh(TriangulationMesh<Vertex2D> mesh, int[] edges)
        {
            //Don't bother with trivial tests since we need two points to constrain an edge
            if (edges.Length < 2)
                return true;

            if (edges.Min() < 0)
                return true;

            if (edges.Max() >= mesh.Verticies.Count)
                return true;

            try
            {
                //OK, constrain edges of the triangulation with non-overlapping edges
                List<IEdgeKey> AddedConstraints = new List<IEdgeKey>();
                RTree.RTree<IEdgeKey> ConstrainedEdgeTree = new RTree<IEdgeKey>();
                int EdgeStart = edges[0];
                for (int i = 1; i < edges.Length; i++)
                {
                    EdgeKey proposedEdge = new EdgeKey(EdgeStart, edges[i]);

                    GridLineSegment proposedSeg = mesh.ToGridLineSegment(proposedEdge);

                    if (IntersectsConstrainedLine(proposedEdge, ConstrainedEdgeTree, mesh))
                        continue;

                    Edge new_edge = new Edge(proposedEdge.A, proposedEdge.B);
                    mesh.AddContrainedEdge(new_edge);
                    ConstrainedEdgeTree.Add(proposedSeg.BoundingBox, new_edge);
                    AddedConstraints.Add(new_edge);

                    //Verify the state of the mesh
                    if (AreTriangulatedVertexEdgesValid(mesh) == false)
                        return false;

                    if (mesh.AnyMeshEdgesIntersect())
                        return false;

                    foreach (Face f in mesh.Faces)
                    {
                        if (mesh.IsClockwise(f))
                            return false;
                    }


                    //Ensure all of the constrained edges are in the mesh
                    foreach (var constraint in AddedConstraints)
                    {
                        if (mesh.Contains(constraint) == false)
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (NotImplementedException e)
            {
                //This currently occurs when a constrained edge passes directly through a vertex
                return true;
            }

            return false;
        }
        */
        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AreTriangulatedVertexEdgesValid(TriangulationMesh<IVertex2D> mesh)
        {
            foreach (var v in mesh.Verticies)
            {
                //Assert.IsTrue(v.Edges.Count > 1); //Every vertex must have at least two edges
                if (v.Edges.Count <= 1)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AreTriangulatedFacesCCW(TriangulationMesh<IVertex2D> mesh)
        {
            foreach (Face f in mesh.Faces)
            { 
                bool IsClockwise = mesh.IsClockwise(f);
                //Assert.IsTrue(IsDelaunay, string.Format("{0} is not a delaunay triangle", f));
                //Assert.IsFalse(IsClockwise, string.Format("{0} is clockwise, incorrect winding.", f));

                if (IsClockwise)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AreTriangulatedFacesColinear(TriangulationMesh<IVertex2D> mesh)
        {
            foreach (Face f in mesh.Faces)
            {
                RotationDirection winding = mesh.Winding(f);
                //Assert.IsTrue(IsDelaunay, string.Format("{0} is not a delaunay triangle", f));
                //Assert.IsFalse(IsClockwise, string.Format("{0} is clockwise, incorrect winding.", f));

                if (winding == RotationDirection.COLINEAR)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AreFacesTriangles(TriangulationMesh<IVertex2D> mesh)
        {
            foreach (Face f in mesh.Faces)
            {
                bool IsTriangle = f.IsTriangle();
                //Assert.IsTrue(IsDelaunay, string.Format("{0} is not a delaunay triangle", f));
                //Assert.IsFalse(IsClockwise, string.Format("{0} is clockwise, incorrect winding.", f));

                if (!IsTriangle)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AreTriangulatedFacesDelaunay(TriangulationMesh<IVertex2D> mesh)
        {
            foreach (Face f in mesh.Faces)
            {
                bool IsDelaunay = mesh.IsTriangleDelaunay(f); 
                //Assert.IsTrue(IsDelaunay, string.Format("{0} is not a delaunay triangle", f));
                //Assert.IsFalse(IsClockwise, string.Format("{0} is clockwise, incorrect winding.", f));

                if (!IsDelaunay)
                    return false;
            }

            return true;
        }

    }
}
