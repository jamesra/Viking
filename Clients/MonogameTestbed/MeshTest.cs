using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using VikingXNAGraphics;
using VikingXNA;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Geometry.Meshing;
using MathNet.Numerics.LinearAlgebra;
using MorphologyMesh;

namespace MonogameTestbed
{
    class MeshTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        VikingXNAGraphics.MeshView<VertexPositionColor> meshView;
        VikingXNAGraphics.MeshView<VertexPositionNormalColor> meshViewWithLighting;

        Mesh3D tetraMesh = new Mesh3D();
        Mesh3D cubeMesh =  new Mesh3D();

        Scene3D Scene;

        LabelView labelCamera;


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        /*
        private Vertex[] CreateCubeVerts(GridVector3 offset)
        {
            Vertex[] verts = new Vertex[] {new Vertex(new GridVector3(-5, -5, -5), new GridVector3(0, 0, 0)),
                                           new Vertex(new GridVector3(5, -5, -5), new GridVector3(0, 1, 0)),
                                           new Vertex(new GridVector3(-5, -5, 5), new GridVector3(0, 0, 1)),
                                           new Vertex(new GridVector3(5, -5, 5), new GridVector3(1, 0, 0)),
                                           new Vertex(new GridVector3(-5, 5, -5), new GridVector3(0, 0, 0)),
                                           new Vertex(new GridVector3(5, 5, -5), new GridVector3(0, 1, 0)),
                                           new Vertex(new GridVector3(-5, 5, 5), new GridVector3(0, 0, 1)),
                                           new Vertex(new GridVector3(5, 5, 5), new GridVector3(1, 0, 0))};

            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Position += offset;
            }

            return verts;
        }

        private Face[] CreateCubeFaces()
        {
            return new Face[] {
                         new Face(0, 2, 3, 1), //Bottom
                         new Face(0, 1, 5, 4), //Front
                         new Face(4, 6, 7, 5), //Top
                         new Face(2, 6, 7, 3), //Back
                         //new Face(2, 7, 3), //Back
                         new Face(1, 3, 7, 5), //Right
                         new Face(0, 4, 6, 2) }; //Left
        }
        */

        

        private Vertex3D[] CreateTetrahedronVerts(GridVector3 offset)
        {
            Vertex3D[] verts = new Vertex3D[] {new Vertex3D(new GridVector3(0, 0, 0), new GridVector3(0, 0, 0)),
                                     new Vertex3D(new GridVector3(0, 1, 0), new GridVector3(0, 1, 0)),
                                     new Vertex3D(new GridVector3(0, 0, 1), new GridVector3(0, 0, 1)),
                                     new Vertex3D(new GridVector3(1, 0, 0), new GridVector3(1, 0, 0)) };

            for(int i = 0; i < verts.Length; i++)
            {
                verts[i].Position += offset; 
            }

            return verts;
        }

        private Face[] CreateTetrahedronFaces()
        {
            return new Face[] {new Face(0,1,2),
                               new Face(0,3,1),
                               new Face(0,2,3),
                               new Face(1,3,2) };
        }

        private Mesh3D CreateTetrahedronMeshModel(GridVector3 offset)
        {
            Mesh3D mesh = new Mesh3D();
            mesh.AddVerticies(CreateTetrahedronVerts(new GridVector3(0, 0, 0)));
            Face[] faces = CreateTetrahedronFaces();
            foreach (Face f in faces)
            {
                mesh.AddFace(f);
            }

            return mesh;
        }

        /*
        private DynamicRenderMesh CreateCubeMeshModel(GridVector3 offset)
        {
            DynamicRenderMesh mesh = new DynamicRenderMesh();
            mesh.AddVertex(CreateCubeVerts(new GridVector3(0, 0, 0)));
            Face[] faces = CreateCubeFaces();
            foreach (Face f in faces)
            {
                mesh.AddFace(f);
            }

            return mesh;
        }*/

        public void Init(MonoTestbed window)
        {
            _initialized = true;
            this.Scene = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D());
            this.meshView = new MeshView<VertexPositionColor>();
            this.meshViewWithLighting = new MeshView<VertexPositionNormalColor>();

            this.Scene.Camera.LookAt = Vector3.Zero;
            this.Scene.Camera.Position = new Vector3(0, -0, -65);

            this.Scene.MaxDrawDistance = 10000;
            /*
          Color[] tetra_colors = new Color[] { Color.Red, Color.Blue, Color.Green, Color.Yellow };
          Color[] cube_colors  = new Color[] { Color.White, Color.Blue, Color.Green, Color.Yellow, Color.Red, Color.Orange, Color.Purple, Color.Black };

          tetraMesh = CreateTetrahedronMeshModel(new GridVector3(-20, 0, 0));

          MeshModel<VertexPositionNormalColor> cubeModel = MorphologyMesh.ShapeMeshGenerator<object>.CreateMeshForBox(new GridBox(new double[] { -5, -5, -5 }, new double[] { 5, 5, 5 }), null, new GridVector3(20, 0, 0)).ToVertexPositionNormalColorMeshModel(cube_colors);
          meshViewWithLighting.models.Add(cubeModel);

          //cubeMesh = CreateCubeMeshModel(new GridVector3(20, 0, 0));

          MeshModel<VertexPositionColor> tetraModel = tetraMesh.ToVertexPositionColorMeshModel(tetra_colors);
          meshView.models.Add(tetraModel);

          //MeshModel<VertexPositionColor> cubeModel = cubeMesh.ToVertexPositionColorMeshModel(cube_colors);
          //meshView.models.Add(cubeModel);

          MeshModel<VertexPositionNormalColor> discModel = MorphologyMesh.ShapeMeshGenerator<object>.CreateMeshForDisc(new GridCircle( new GridVector2(20, 20), 10), 0, 10, 16, null, GridVector3.Zero).ToVertexPositionNormalColorMeshModel(Color.Red);
          meshViewWithLighting.models.Add(discModel);

          DynamicRenderMesh boxMesh = MorphologyMesh.ShapeMeshGenerator<object>.CreateMeshForBox(new GridBox(new double[] { 10, 0, 20 }, new double[] { 15, 5, 27 }), null, GridVector3.Zero);
          boxMesh.RecalculateNormals(); //Make sure it looks the same as the cube model above

          MeshModel<VertexPositionNormalColor> boxModel = boxMesh.ToVertexPositionNormalColorMeshModel(cube_colors);
          meshViewWithLighting.models.Add(boxModel);

          MeshModel<VertexPositionNormalColor> polyModel = MorphologyMesh.ShapeMeshGenerator<object>.CreateMeshForPolygonSlab(StandardGeometryModels.CreateTestPolygon(new GridVector2(20, -40)), -5, 3, null, GridVector3.Zero).ToVertexPositionNormalColorMeshModel(Color.Aqua);
          meshViewWithLighting.models.Add(polyModel);

          MeshModel<VertexPositionNormalColor> circleModel = BuildCircleConvexHull(new GridCircle(new GridVector2(20, -20), 5));
          meshViewWithLighting.models.Add(circleModel);
          */

            this.Scene.Camera.Position = new Vector3(29, -13.5f, 24.75f);
            this.Scene.Camera.Rotation = new Vector3(2.5f, 2.055f, 0);
            /*
                        foreach (MeshModel<VertexPositionNormalColor> model in BuildSmoothMeshTwoNonOverlappingCircles(new GridVector3(0, 0, 0)))
                        {
                            meshViewWithLighting.models.Add(model);
                        }


                        foreach (MeshModel<VertexPositionNormalColor> model in BuildSmoothMeshCircleBranchOfOneOverlapping(new GridVector3(0, 0, 0)))
                        {                
                            meshViewWithLighting.models.Add(model);
                        }

                        */
            /*
            foreach (MeshModel<VertexPositionNormalColor> model in BuildSmoothMeshLine(GridVector3.Zero))
            {
                meshViewWithLighting.models.Add(model);
            }
            */


            //meshViewWithLighting.models.Add(BuildPolygonBranchCenter(GridVector3.Zero));
            //Add a simple shape that should always be correct to test simple process and terminal rendering
            //meshViewWithLighting.models.Add(BuildSmoothMeshTwoNonOverlappingCircles(new GridVector3(50,0,0)));

            //meshViewWithLighting.models.Add(BuildSmoothMeshTwoPolygons(GridVector3.Zero));
            //meshViewWithLighting.models.Add(BuildPolygonBranchCenter(GridVector3.Zero));
            meshViewWithLighting.models.Add(BuildSmoothMeshFromSharedModel(GridVector3.Zero));

            labelCamera = new LabelView("", new GridVector2(-70, 0));
        }

        public void UnloadContent(MonoTestbed window)
        {
            //this.Scene.SaveCamera(TestMode.MESH);
        }

        private MeshModel<VertexPositionNormalColor> BuildSmoothMesh1(GridVector3 translate)
        {
            //Create three simple polygons and add them to the graph
            IShape2D[] shapes = {new GridCircle(0,0,10),
                                 new GridCircle(-2,0,11),
                                 
                                 new GridPolygon(new GridVector2[] {
                                                    new GridVector2(-10,-10),
                                                    new GridVector2(-10,10),
                                                    new GridVector2(10,10),
                                                    new GridVector2(10,-10),
                                                    new GridVector2(-10,-10)}),
                                 new GridCircle(0,7, 5),
                                 new GridCircle(-5, -5, 5),
                                 new GridCircle(-15, -15, 5)
                                };
            double[] ZLevels = new double[] { 0, 5, 10, 15, 15, 20 };

            MeshEdge[] edges = new MeshEdge[] {
                                         new MeshEdge(0, 1),
                                         new MeshEdge(1, 2),
                  new MeshEdge(2, 3),
                  new MeshEdge(2, 4),
                  new MeshEdge(4,5)
            };

            MeshGraph graph = StandardModels.BuildMeshGraph(shapes, ZLevels, edges, 5.0, translate);

            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildSmoothMeshFromSharedModel(GridVector3 translate)
        {
            //Create three simple polygons and add them to the graph
            MeshGraph graph = StandardModels.BuildMeshGraph(StandardModels.SharedModelPolygons, StandardModels.SharedModelZ, StandardModels.SharedModelEdges, 5.0, translate);

            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildSmoothMeshTwoPolygons(GridVector3 translate)
        {
            //Create three simple polygons and add them to the graph
            IShape2D[] shapes = {new GridPolygon(new GridVector2[] {
                                                    new GridVector2(-10,-10),
                                                    new GridVector2(-10,10),
                                                    new GridVector2(10,10),
                                                    new GridVector2(10,-10),
                                                    new GridVector2(-10,-10)}),
                                 StandardGeometryModels.CreateTestPolygon(false)
                                };
            double[] ZLevels = new double[] { 0, 5, 10, 15, 15, 20 };

            MeshEdge[] edges = new MeshEdge[] {
                                         new MeshEdge(0, 1)
            };

            MeshGraph graph = StandardModels.BuildMeshGraph(shapes, ZLevels, edges, 5.0, translate);

            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildSmoothMeshTwoNonOverlappingCircles(GridVector3 translate)
        {  
            //Create three simple polygons and add them to the graph
            IShape2D[] shapes = { new GridCircle(-5, -5, 5),
                                  new GridCircle(-15, -15, 5)
                                };
            double[] ZLevels = new double[] { 0, 5, 10, 15, 15, 20 };

            MeshEdge[] edges = new MeshEdge[] {
                                         new MeshEdge(0, 1)
            };


            MeshGraph graph = StandardModels.BuildMeshGraph(shapes, ZLevels, edges, 5.0, translate);

            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildSmoothMeshCircleBranchOfOneOverlapping(GridVector3 translate)
        {
            //Create three simple polygons and add them to the graph
            IShape2D[] shapes = { new GridCircle(-5, -5, 5),
                                  new GridCircle(-15, -15, 5),
                                  new GridCircle(0, 0, 5)
                                };
            double[] ZLevels = new double[] { 0, 5, 5, 15, 15, 20 };

            MeshEdge[] edges = new MeshEdge[] {
                                         new MeshEdge(0, 1),
                                         new MeshEdge(0,2)
            };
            
            MeshGraph graph = StandardModels.BuildMeshGraph(shapes, ZLevels, edges, 5.0, translate);

            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildSmoothMeshCircleBranchOfOneOverlappingButTall(GridVector3 translate)
        {
            //Create three simple polygons and add them to the graph
            IShape2D[] shapes = { new GridCircle(0, 0, 10),
                                  new GridCircle(0, 0, 5),
                                  new GridCircle(-15, 0, 5),
                                  new GridCircle(0, 15, 5),
                                  new GridCircle(-15, -20, 5),
                                  new GridCircle(0, 17.5, 5),
                                  new GridCircle(0, 17.5, 5)

                                };
            double[] ZLevels = new double[] { 0, 10, 20, 20, 30, 30, 40 };

            MeshEdge[] edges = new MeshEdge[] {
                                         new MeshEdge(0, 1),
                                         new MeshEdge(1,2),
                                         new MeshEdge(1,3),
                                         new MeshEdge(2,4),
                                         new MeshEdge(3,5),
                                         new MeshEdge(5,6)
            };

            MeshGraph graph = StandardModels.BuildMeshGraph(shapes, ZLevels, edges, 5.0, translate);
            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildSmoothMeshCircleXBranchOfOneOverlappingButTall(GridVector3 translate)
        {
            //Create three simple polygons and add them to the graph
            IShape2D[] shapes = { new GridCircle(0, 0, 10), //
                                  new GridCircle(0, 0, 5), //Center
                                  new GridCircle(-15, 0, 5),
                                  new GridCircle(0, 15, 5),
                                  new GridCircle(-15, -20, 5),
                                  new GridCircle(0, 17.5, 5),
                                  new GridCircle(0, 17.5, 5),
                                  new GridCircle(-10, -10, 5)

                                };
            double[] ZLevels = new double[] { 0, 10, 20, 20, 30, 30, 40, 0 };

            MeshEdge[] edges = new MeshEdge[] {
                                         new MeshEdge(0, 1),
                                         new MeshEdge(1,2),
                                         new MeshEdge(1,3),
                                         new MeshEdge(2,4),
                                         new MeshEdge(3,5),
                                         new MeshEdge(5,6),
                                         new MeshEdge(1,7)
            };

            MeshGraph graph = StandardModels.BuildMeshGraph(shapes, ZLevels, edges, 5.0, translate);
            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildSmoothMeshCircleDoubleBranchOfOneOverlappingButTall(GridVector3 translate)
        {
            //Create three simple polygons and add them to the graph
            IShape2D[] shapes = { new GridCircle(0, 0, 10), //
                                  new GridCircle(0, 0, 5), //Center
                                  new GridCircle(-15, 0, 5), //Branch A
                                  new GridCircle(0, 15, 5), //Branch B
                                  new GridCircle(-15, -20, 5), 
                                  new GridCircle(0, 17.5, 5), //Branch B Upper Cap
                                  new GridCircle(0, 17.5, 5),
                                  new GridCircle(-15, 20, 5),
                                  new GridCircle(0, 17.5, 5) //Branch B Lower Cap

                                };
            double[] ZLevels = new double[] { 0, 10, 20, 20, 30, 30, 40, 40, 0 };

            MeshEdge[] edges = new MeshEdge[] {
                                         new MeshEdge(0, 1),
                                         new MeshEdge(1,2),
                                         new MeshEdge(1,3),
                                         new MeshEdge(2,4),
                                         new MeshEdge(3,5),
                                         new MeshEdge(5,6),
                                         new MeshEdge(2,7),
                                         new MeshEdge(3,8)
            };

            MeshGraph graph = StandardModels.BuildMeshGraph(shapes, ZLevels, edges, 5.0, translate);
            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildSmoothMeshLine(GridVector3 translate)
        {
            //Create three simple polygons and add them to the graph
            IShape2D[] shapes = { new GridPolyline(new GridVector2[] { new GridVector2(0, 0), new GridVector2(0, 10) }),
                                  new GridPolyline(new GridVector2[] { new GridVector2(0, 0), new GridVector2(5, 5), new GridVector2(1, 15) })            
                                };

            double[] ZLevels = new double[] { 0, 10, 20, 20, 30, 30, 40 };

            MeshEdge[] edges = new MeshEdge[] {
                                         new MeshEdge(0, 1) //,
                                         //new MeshEdge(1,2),
                                         //new MeshEdge(1,3),
                                         //new MeshEdge(2,4),
                                         //new MeshEdge(3,5),
                                         //new MeshEdge(5,6)
            };

            MeshGraph graph = StandardModels.BuildMeshGraph(shapes, ZLevels, edges, 5.0, translate);
            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildPolygonBranchCenter(GridVector3 translate)
        {
            //Create three simple polygons and add them to the graph
            IShape2D[] shapes = { StandardGeometryModels.CreateBoxPolygon(new GridRectangle(new GridVector2(0, 0), new GridVector2(4,6))), //Center
                                  new GridCircle(-15, -15, 5), //Upper A
                                  new GridCircle(0, 0, 5),     //Upper B
                                  StandardGeometryModels.CreateBoxPolygon(new GridRectangle(new GridVector2(-10, 0), new GridVector2(-4,4))),
                                  StandardGeometryModels.CreateBoxPolygon(new GridRectangle(new GridVector2(-10, 0), new GridVector2(-4,4))),
                                  StandardGeometryModels.CreateBoxPolygon(new GridRectangle(new GridVector2(20, 20), new GridVector2(24,30))),
                                  StandardGeometryModels.CreateBoxPolygon(new GridRectangle(new GridVector2(-15, -15), new GridVector2(-10,-10))),
                                  StandardGeometryModels.CreateBoxPolygon(new GridRectangle(new GridVector2(-25, -25), new GridVector2(-20,-20)))
                                };
            double[] ZLevels = new double[] { 0, 5, 5, -15, -25,-25, 15, 15};

            MeshEdge[] edges = new MeshEdge[] {
                                         new MeshEdge(0, 1),
                                         new MeshEdge(0,2),
                                         new MeshEdge(0,3),
                                      //   new MeshEdge(3,4),
                                         new MeshEdge(0,5),
                                         new MeshEdge(1,6),
                                         new MeshEdge(1,7)
            };

            MeshGraph graph = StandardModels.BuildMeshGraph(shapes, ZLevels, edges, 5.0, translate);

            Mesh3D<IVertex3D<ulong>> mesh = SmoothMeshGenerator.Generate(graph);
            List<MeshModel<VertexPositionNormalColor>> listMeshModels = new List<MeshModel<VertexPositionNormalColor>>();
            return mesh.ToVertexPositionNormalColorMeshModel(Color.Yellow);
        }

        private MeshModel<VertexPositionNormalColor> BuildCircleConvexHull(ICircle2D circle)
        { 
            IVertex3D<object>[] verticies = MorphologyMesh.ShapeMeshGenerator<Vertex3D<object>,object>.CreateVerticiesForCircle(circle, 0, 16, null, GridVector3.Zero);

            GridVector2[] verts2D = verticies.Select(v => new GridVector2(v.Position.X, v.Position.Y)).ToArray();

            int[] cv_idx;
            GridVector2[] cv_verticies = verts2D.ConvexHull(out cv_idx);

            GridPolygon poly = new GridPolygon(cv_verticies);
            return MorphologyMesh.ShapeMeshGenerator<Vertex3D<object>,object>.CreateMeshForPolygon(poly, 0, null, GridVector3.Zero).ToVertexPositionNormalColorMeshModel(Color.Gold);
        }

        public void Update()
        {
            StandardCameraManipulator.Update(this.Scene.Camera);
            GamePadState state = GamePad.GetState(PlayerIndex.One);
            
            if (state.Buttons.Y == ButtonState.Pressed)
            {
                meshView.WireFrame = !meshView.WireFrame;
                meshViewWithLighting.WireFrame = meshView.WireFrame;
            }            

            labelCamera.Text = string.Format("{0} {2}", Scene.Camera.Position, Scene.Camera.LookAt, Scene.Camera.Rotation);
        }

        public void Draw(MonoTestbed window)
        {
            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Color.DarkGray, float.MaxValue, 0);
         
            DepthStencilState dstate = new DepthStencilState();
            dstate.DepthBufferEnable = true;
            dstate.StencilEnable = false;
            dstate.DepthBufferWriteEnable = true;
            dstate.DepthBufferFunction = CompareFunction.LessEqual; 

            window.GraphicsDevice.DepthStencilState = dstate;
            //window.GraphicsDevice.BlendState = BlendState.Opaque;
            meshView.Draw(window.GraphicsDevice, this.Scene);
            meshViewWithLighting.Draw(window.GraphicsDevice, this.Scene);

            
            window.spriteBatch.Begin();
            labelCamera.Draw(window.spriteBatch, window.fontArial, window.Scene);
            window.spriteBatch.End(); 
        }



    }
}
