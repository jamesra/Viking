using FsCheck;
using Geometry;
using Geometry.JSON;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public static Property ClassifySize(this Property prop, int nVerts, string Label = "")
        {

            return prop.Classify(nVerts == 0, Label + " 0")
               .Classify(nVerts > 0 && nVerts <= 3, Label + "1-3")
               .Classify(nVerts > 3 && nVerts <= 10, Label + "3 - 10")
               .Classify(nVerts > 10 && nVerts <= 32, Label + "11 - 32")
               .Classify(nVerts > 32 && nVerts <= 64, Label + "33 - 64")
               .Classify(nVerts > 64 && nVerts <= 128, Label + "65 - 128")
               .Classify(nVerts > 128 && nVerts <= 256, Label + "129 - 256")
               .Classify(nVerts > 256 && nVerts <= 512, Label + "257 - 512")
               .Classify(nVerts > 512 && nVerts <= 1024, Label + "513 - 1024")
               .Classify(nVerts > 1024, Label + "1024+");
        }
    }

    public static class TriangulationValidation
    {
        public static Property ValidatePolygonTriangulation(this GridPolygon p, IReadOnlyMesh2D<IVertex2D> mesh, List<IEdgeKey> expectedConstrainedEdges)
        {
            bool edgesIntersect = mesh.AnyMeshEdgesIntersect();
            bool facesCCW = mesh.AreTriangulatedFacesCCW();
            bool facesColinear = mesh.AreTriangulatedFacesColinear();
            bool facesAreTriangles = mesh.AreFacesTriangles();
            bool[] meshHasConstrainedEdges = DelaunayTest.MeshContainsEdges(mesh, expectedConstrainedEdges);
            bool meshHasAllConstrainedEdges = meshHasConstrainedEdges.All(b => b);
            bool[] meshConstrainedEdgesHaveOneFace = DelaunayTest.MeshEdgeFaceCount(mesh, expectedConstrainedEdges, 1);
            bool meshConstrainedEdgesAllHaveOneFace = meshConstrainedEdgesHaveOneFace.All(b => b);

            bool pass = (edgesIntersect == false) && facesCCW && (facesColinear == false) && facesAreTriangles && meshHasAllConstrainedEdges && meshConstrainedEdgesAllHaveOneFace;

            return ((edgesIntersect == false).Label("Edges intersect"))
                    .And(facesCCW.Label("Face is clockwise"))
                    .And((facesColinear == false).Label("Face is colinear"))
                    .And(facesAreTriangles.Label("Face is not a triangle"))
                    .And(DelaunayTest.MeshContainsEdgesProperty(mesh, expectedConstrainedEdges))
                    .And(DelaunayTest.MeshEdgeFaceCountProperty(mesh, expectedConstrainedEdges, 1))
                    .ClassifySize(p.TotalUniqueVerticies, "Unique Verts:")
                    .Label(p.ToJSON());
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
        public void DelaunayGeneratorParameterTestFromPoints()
        {
            //A second pass implementation that generates entire meshes as random parameters and not sets of points that I must convert to meshes
            GeometryArbitraries.Register();
            GeometryMeshingArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 10;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 10;
            configuration.Replay = Global.StdGenSeed;

            Prop.ForAll<GridVector2>((point) => System.Diagnostics.Trace.WriteLine(point.ToString())).Check(configuration); // IsDelaunay(GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh(points.Select(p => new Vertex2D(p)).ToArray()))).Check(configuration);


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
        public void DelaunayGeneratorParameterTestFromModel()
        {
            //A second pass implementation that generates entire meshes as random parameters and not sets of points that I must convert to meshes
            GeometryArbitraries.Register();
            GeometryMeshingArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 100;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 10;

            //Prop.ForAll<TriangulationMesh<Vertex2D>>((mesh) => IsDelaunay(mesh)).Check(configuration);
            Prop.ForAll<TriangulationMesh<IVertex2D>>((mesh) => IsDelaunay(mesh)).Check(configuration);

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
            configuration.MaxNbOfTest = 100;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 1;


            //Func<int, int, ConstrainedDelaunaySpec> func = (nVerts, nEdges) => new ConstrainedDelaunaySpec(nVerts, nEdges);
            Prop.ForAll<GridVector2[], ushort[]>((points, edges) =>
            {
                var mesh = GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(points.Select(v => new Vertex2D(v, null)).ToArray());
                var edge_configuration = Configuration.QuickThrowOnFailure;
                edge_configuration.MaxNbOfTest = 2;
                edge_configuration.QuietOnSuccess = true;
                edge_configuration.StartSize = edges.Length - 1;

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
                model_configuration.StartSize = model.ConstraintEdges.Count / 2; //Set the number of edges so the correct number of Commands are generated

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

        static public bool[] MeshEdgeFaceCount(IReadOnlyMesh<IVertex> mesh, IReadOnlyList<IEdgeKey> edges, int nFaces)
        {
            return edges.Select(e => mesh.Contains(e) && mesh[e].Faces.Count == nFaces).ToArray();
        }

        static public Property MeshEdgeFaceCountProperty(IReadOnlyMesh<IVertex> mesh, IReadOnlyList<IEdgeKey> edges, int nFaces)
        {
            var hasEdges = edges.Select(e => (mesh.Contains(e) && mesh[e].Faces.Count == nFaces).Label(string.Format("Edge {0} has {1} face needs {2}", e, mesh.Contains(e) ? mesh[e].Faces.Count : -1, nFaces))).ToArray();
            return hasEdges.Aggregate((a, b) => a.And(b));
        }

        static public bool[] MeshContainsEdges(IReadOnlyMesh<IVertex> mesh, IReadOnlyList<IEdgeKey> edges)
        {
            return edges.Select(e => mesh.Contains(e)).ToArray();
        }

        static public Property MeshContainsEdgesProperty(IReadOnlyMesh<IVertex> mesh, IReadOnlyList<IEdgeKey> edges)
        {
            var hasEdges = edges.Select(e => mesh.Contains(e).Label(string.Format("Has Edge {0}", e))).ToArray();
            return hasEdges.Aggregate((a, b) => a.And(b));
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
            Property facesDelaunay = mesh.AreTriangulatedFacesDelaunay(out bool facesDelaunayResult);
            bool facesCCW = mesh.AreTriangulatedFacesCCW();
            bool facesColinear = mesh.AreTriangulatedFacesColinear();
            bool vertEdges = mesh.AreTriangulatedVertexEdgesValid() || mesh.Verticies.Count < 3;
            bool facesAreTriangles = mesh.AreFacesTriangles();
            bool success = (edgesIntersect == false) && facesDelaunayResult && facesCCW && vertEdges && facesAreTriangles;

            if (success == false)
                System.Threading.Thread.Sleep(200); //Sleep to allow screent to update 

            int nVerts = mesh.Verticies.Count;
            return (edgesIntersect == false).Label("Edges intersect")
               .And(facesDelaunay)//.Label("Faces not Delaunay"))
               .And(facesCCW.Label("Faces Clockwise"))
               .And((facesColinear == false).Label("Faces colinear"))
               .And(facesAreTriangles.Label("Faces aren't triangles"))
               .And(vertEdges.Label("Verts with 0 or 1 edges"))
               .ClassifyMeshSize(nVerts)
               .Label(mesh.ToJSON());
        }


        /*
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
                
                if (mesh.AreTriangulatedVertexEdgesValid() == false)
                    return false;

                if (mesh.AnyMeshEdgesIntersect())
                    return false;

                if (mesh.AreTriangulatedFacesCCW() == false)
                    return false;

                if (mesh.AreTriangulatedFacesDelaunay() == false)
                    return false;

                return true;
            }
            catch (ArgumentException e)
            {
                return false;
            }
        }*/

        [TestMethod]
        public void TriangulatePolygonTest()
        {
            TriangulatePolygonTest(null);
        }

        public void TriangulatePolygonTest(TriangulationMesh<IVertex2D<PointIndex>>.ProgressUpdate OnProgress = null)
        {
            GeometryArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 200;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 10;
            configuration.Replay = Global.StdGenSeed;

            Prop.ForAll<GridPolygon>(p =>
            {
                try
                {
                    var mesh = TriangulatePoly(p, out List<IEdgeKey> expectedConstrainedEdges, OnProgress);
                    return p.ValidatePolygonTriangulation(mesh, expectedConstrainedEdges);
                }
                catch (Exception e)
                {
                    return (false.Label(p.ToJSON()))
                            .Label(e.ToString())
                            .ClassifySize(p.TotalUniqueVerticies, "Unique Verts:");
                }
            }).Check(configuration);// QuickCheckThrowOnFailure();    
        }


        public static TriangulationMesh<IVertex2D<PointIndex>> TriangulatePoly(GridPolygon p, out List<IEdgeKey> expectedConstrainedEdges, TriangulationMesh<IVertex2D<PointIndex>>.ProgressUpdate OnProgress = null)
        {
            expectedConstrainedEdges = new List<IEdgeKey>();

            p = p.Translate(-p.Centroid);
            //var mesh = p.Triangulate(p.ExteriorRing.Distinct().Select(p => new Vertex2D(p)).ToArray(),null,OnProgress);
            var mesh = p.Triangulate(0, OnProgress); // Geometry.Meshing.MeshExtensions.Triangulate(p, 0, OnProgress);//p.ExteriorRing.Select(t => new Vertex2D(t)).ToArray(), OnProgress);
            var PosToVert = mesh.Verticies.ToDictionary(v => v.Position);

            foreach (GridLineSegment s in p.AllSegments)
            {
                EdgeKey key = new EdgeKey(PosToVert[s.A].Index, PosToVert[s.B].Index);
                expectedConstrainedEdges.Add(key);
            }

            return mesh;
        }



        //Todo: Make a generator that pulls polygons from the database and triangulates them

        [TestMethod]
        public void TriangulatePolygonTestWithInteriorPoints()
        {
            TriangulatePolygonTestWithInteriorPoints(null);
        }

        public void TriangulatePolygonTestWithInteriorPoints(TriangulationMesh<IVertex2D<int>>.ProgressUpdate OnProgress = null)
        {
            GeometryArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 300;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 2;
            configuration.Replay = Global.StdGenSeed;



            Prop.ForAll<GridPolygon, GridVector2[]>((p, interior) =>
            {
                GridVector2 pCentroid = p.Centroid;
                p = p.Translate(-pCentroid);

                interior = interior.Select(i => i - pCentroid).ToArray();

                GridVector2[] qualifiedPoints = interior.Where(i => p.ContainsExt(i) == OverlapType.CONTAINED).ToArray();

                //var mesh = p.Triangulate(p.ExteriorRing.Distinct().Select(p => new Vertex2D(p)).ToArray(),null,OnProgress);
                var mesh = Geometry.Meshing.MeshExtensions.Triangulate(p.ExteriorRing.Distinct().Select((t, i) => new Vertex2D(i, t)).ToArray(), qualifiedPoints.Select(x => new Vertex2D(x)).ToArray(), OnProgress);

                if (mesh.Verticies.Count != (p.ExteriorRing.Length - 1) + qualifiedPoints.Length)
                    return false.Label("Mesh did not contain all verticies")
                            .ClassifyMeshSize((p.ExteriorRing.Length - 1) + qualifiedPoints.Length)
                            .Label(p.ToJSON());

                var PosToVert = mesh.Verticies.ToDictionary(v => v.Position);
                List<IEdgeKey> expectedConstrainedEdges = new List<IEdgeKey>();

                //This test has false negative failures because when a corresponding edge perfectly intersects a vertex it is broken
                //into two halves and the test does not capture that event.

                foreach (GridLineSegment s in p.ExteriorSegments)
                {
                    if (PosToVert.ContainsKey(s.A) == false)
                        return false.Label(string.Format("PosToVert does not contain {0}", s.A))
                            .ClassifyMeshSize(mesh.Verticies.Count);
                    if (PosToVert.ContainsKey(s.B) == false)
                        return false.Label(string.Format("PosToVert does not contain {0}", s.B))
                            .ClassifyMeshSize(mesh.Verticies.Count);

                    EdgeKey key = new EdgeKey(PosToVert[s.A].Index, PosToVert[s.B].Index);
                    expectedConstrainedEdges.Add(key);
                }


                return p.ValidatePolygonTriangulation(mesh, expectedConstrainedEdges);
                /*
                bool edgesIntersect = mesh.AnyMeshEdgesIntersect();
                bool facesCCW = AreTriangulatedFacesCCW(mesh);
                bool facesColinear = AreTriangulatedFacesColinear(mesh);
                bool facesAreTriangles = DelaunayTest.AreFacesTriangles(mesh);
                bool[] meshHasConstrainedEdges = DelaunayTest.MeshContainsEdges(mesh, expectedConstrainedEdges);
                bool meshHasAllConstrainedEdges = meshHasConstrainedEdges.All(b => b);
                bool[] meshConstrainedEdgesHaveOneFace = DelaunayTest.MeshEdgeFaceCount(mesh, expectedConstrainedEdges, 1);
                bool meshConstrainedEdgesAllHaveOneFace = meshConstrainedEdgesHaveOneFace.All(b => b);

                bool pass = (edgesIntersect == false) && facesCCW && (facesColinear == false) && facesAreTriangles && meshHasAllConstrainedEdges && meshConstrainedEdgesAllHaveOneFace;

                return ((edgesIntersect == false).Label("Edges intersect"))
                        .And(facesCCW.Label("Face is clockwise"))
                        .And((facesColinear == false).Label("Face is colinear"))
                        .And(facesAreTriangles.Label("Face is not a triangle"))
                        .And(DelaunayTest.MeshContainsEdgesProperty(mesh, expectedConstrainedEdges))
                        .And(DelaunayTest.MeshEdgeFaceCountProperty(mesh, expectedConstrainedEdges, 1))
                        .ClassifySize(p.TotalUniqueVerticies, "Num Verts:")
                        .ClassifySize(qualifiedPoints.Length, "Num Interior:")
                        .Label(p.ToJSON());
                        */
            }).Check(configuration);//.VerboseCheckThrowOnFailure();

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


    }
}
