using AnnotationVizLib;
using ColladaIO;
using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MorphologyMesh;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;


namespace MonogameTestbed
{
    class BajajMultiOTVAssignmentView
    {
        public readonly GridPolygon[] Polygons = null;
        public readonly double[] PolyZ = null;
        //public PointSetView[] PolyPointsView = null;
        public PointSetView IncompletedVertexView = null;

        public CullMode CullMode = CullMode.CullCounterClockwiseFace;

        public ConcurrentQueue<BajajGeneratorMesh> CompletedMeshes = new ConcurrentQueue<BajajGeneratorMesh>();

        public int? iShownLineView = null;
        public List<LineSetView> listLineViews = new List<LineSetView>();
        public bool ShowLines
        {
            get
            {
                return iShownLineView.HasValue;
            }
        }

        //private LineSetView lineViews = new LineSetView();
        //private LineSetView unfiltered_lineViews = new LineSetView();
        //List<LineView> polyRingViews = null;
        public PointSetView MeshVertsView = null;

        /// <summary>
        /// The position of this mesh in volume space. 
        /// </summary>
        public GridVector2 Position => Graph.BoundingBox.CenterPoint.XY();

        readonly PolygonSetView PolyViews;
        readonly List<LineView> OTVTableView = null;
         
        //BajajGeneratorMesh FirstPassTriangulation = null;

        public List<RegionView> RegionViews = new List<RegionView>();

        public int? iShownMesh = null;
        public List<MeshView<VertexPositionColor>> MeshViews = new List<MeshView<VertexPositionColor>>();
        public bool ShowMesh
        {
            get
            {
                return iShownMesh.HasValue;
            }
        } 


        //MeshModel<VertexPositionColor> meshViewModel = null;


        MeshView<VertexPositionColor> SliceMeshView = null;
        MeshView<VertexPositionNormalColor> CompositeMeshView = null;

        //public SliceGraphMeshModel CompositeMeshModel = null;

        public MeshAssemblyPlanner meshAssemblyPlan = null;
        public MeshAssemblyPlannerCompletedView meshCompletedView = null;
        public MeshAssemblyPlannerIncompleteView meshIncompleteView = null;

        //LineView[] lineViews = null;

        public int? iShownRegion = null;
        readonly List<LineSetView> RegionPolygonViews;
        readonly List<LabelView> RegionLabelViews;

        public bool ShowFaces = false;
        public bool ShowPolygons = true;
        public bool ShowRegionPolygons {
            get
            {
                return iShownRegion.HasValue;
            }
        }

        public bool ShowCompletedVerticies = true;
        public bool ShowAllEdges = false;


        /// <summary>
        /// True if we show composite mesh, false if we show the slice mesh
        /// </summary>
        public bool ShowCompositeMesh = true;

        public IndexLabelType VertexLabelType
        {
            get
            {
                if(PolyViews != null)
                    return PolyViews.PointLabelType;

                return IndexLabelType.NONE;
            }
            set
            {
                if(PolyViews != null)
                    PolyViews.PointLabelType = value;
            }
        }
        
        public bool ShowPolyIndexLabels
        {
            get
            {
                return PolyViews.LabelPolygonIndex;
            }
        }

        public bool ShowMeshIndexLabels
        {
            get
            {
                return PolyViews.LabelIndex;
            }
        }


        public bool ShowPolyPositionLabels
        {
            get
            {
                return PolyViews.LabelPosition;
            }
        }

        public readonly MorphologyGraph Graph;
        /// <summary>
        /// Used to lock the mesh views for individual slices
        /// </summary>
        private readonly object drawlock = new object();
        readonly System.Threading.Thread BuildCompositeThread = null;
        //SliceGraph sliceGraph;

        public BajajMultiOTVAssignmentView(MorphologyGraph graph)
        {
            ///Takes a set of polygons and Z values and generates a meshView
            Graph = graph;

            /*
            Trace.WriteLine("Begin Slice graph construction");
            sliceGraph = SliceGraph.Create(graph, 2.0);
            Trace.WriteLine("End Slice graph construction");
            */

            ResetMesh();


            //BuildCompositeThread = new System.Threading.Thread(this.MeshCompositeTask);
            //BuildCompositeThread.IsBackground = true;
            //BuildCompositeThread.Start();
        }


        /// <summary>
        /// Called when the test window is closed
        /// </summary>
        public void OnUnloadContent()
        {
            if (BuildCompositeThread == null)
                return;

            BuildCompositeThread.Abort();
        }

        private void OnSliceCompleted(BajajGeneratorMesh mesh, bool Success)
        {
            this.AddMesh(mesh, Success);
        }

        private void AddMesh(BajajGeneratorMesh mesh, bool Success)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                meshAssemblyPlan.OnMeshCompleted(mesh, Success);
                //CompletedMeshes.Enqueue(mesh);
            });

            //MeshModel<VertexPositionColor> meshViewModel = BajajOTVAssignmentView.CreateFaceView(mesh);
            //CompletedMeshes.Enqueue(mesh);
            /*lock (drawlock)
            { 
                //SliceMeshView.models.Add(meshViewModel);
                CompositeMeshModel.AddSlice(mesh);
            }
            */
        }

        /// <summary>
        /// Called before GenerateMesh to reset the class views.
        /// </summary>
        internal void ResetMesh()
        {
            SliceMeshView = new MeshView<VertexPositionColor>
            {
                Name = "Slice Mesh"
            };

            this.RegionViews.Clear();
            this.listLineViews.Clear();
            this.MeshViews.Clear();

            lock (drawlock)
            {
                MeshViews.Add(SliceMeshView);
            }

            CompositeMeshView = new MeshView<VertexPositionNormalColor>
            {
                Name = "Composite Mesh"
            };
        }

        internal async Task GenerateMesh()
        {
            if (MeshViews.Count > 0)
                ResetMesh();
            
            MorphologyGraph graph = this.Graph;
            //CompositeMeshModel = new SliceGraphMeshModel();
            //CompositeMeshView.models.Add(CompositeMeshModel.model);

            Trace.WriteLine("Begin Slice graph construction");
            SliceGraph sliceGraph = await SliceGraph.Create(graph, 2.0);
            Trace.WriteLine("End Slice graph construction");

            meshAssemblyPlan = new MeshAssemblyPlanner(sliceGraph);

            meshIncompleteView = new MeshAssemblyPlannerIncompleteView(meshAssemblyPlan, sliceGraph);
            meshCompletedView = new MeshAssemblyPlannerCompletedView(meshAssemblyPlan, graph.BoundingBox.CenterPoint)
            {
                Color = ColorExtensions.Random()
            };

            List<BajajGeneratorMesh> meshes = BajajMeshGenerator.ConvertToMesh(sliceGraph, OnSliceCompleted);

            //If we have fewer region views, reset the region view index
            if(iShownRegion.HasValue && iShownRegion.Value > RegionViews.Count)
            {
                iShownRegion = null; 
            }

            if(iShownLineView == null)
            {
                iShownLineView = listLineViews.Count - 1;
            }

            if (iShownMesh == null)
            {
                lock (drawlock)
                {
                    iShownMesh = MeshViews.Count - 1;
                }
            }
        }
        

        public void Draw(MonoTestbed window, Scene scene)
        {
            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Color.DarkGray, float.MaxValue, 0);
            StringBuilder ViewLabels = new StringBuilder();

            if (RegionViews != null  && ShowRegionPolygons && (iShownRegion.HasValue && iShownRegion.Value  < RegionViews.Count))
            {
                RegionViews[iShownRegion.Value].Draw(window, scene);
                ViewLabels.AppendLine("Region Pass #" + iShownRegion.Value);
            }


            /*lock (drawlock)
            {
            */
                if (ShowCompositeMesh == false)
                {
                    if (MeshViews != null  && ShowMesh && (iShownMesh.HasValue && iShownMesh.Value < MeshViews.Count))
                    {
                        lock (drawlock)
                        {
                            MeshViews[iShownMesh.Value].Draw(window.GraphicsDevice, window.Scene, CullMode.None);
                            ViewLabels.AppendLine(MeshViews[iShownMesh.Value].Name);
                        }
                    }
                }
                else
                {
                    if (CompositeMeshView != null)
                    {
                        CompositeMeshView.Draw(window.GraphicsDevice, window.Scene, CullMode.None);
                        ViewLabels.AppendLine(CompositeMeshView.Name);
                    }
                }
            //}


            if(listLineViews != null && ShowLines && (iShownLineView.HasValue && iShownLineView.Value < listLineViews.Count))
            {
                int iShownLine = iShownLineView.Value;
                LineSetView lineView = listLineViews[iShownLine];

                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, 0);
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, lineView.LineViews.ToArray());
                window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, float.MaxValue, 0);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 10);
                //CurveLabel.Draw(window.GraphicsDevice, window.Scene, window.spriteBatch, window.fontArial, window.curveManager, lineView.LineLables.ToArray());
                foreach (var labelsByFont in lineView.LineLabels.GroupBy(l => l.font))
                {
                    LabelView.Draw(window.spriteBatch, labelsByFont.Key, window.Scene, labelsByFont.ToArray());
                }

                ViewLabels.AppendLine(lineView.Name);
            }
            /*
            if (lineViews != null && ShowPolygons && !ShowRegionPolygons)
            {
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, 0);
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, lineViews.LineViews.ToArray());
                window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, float.MaxValue, 0);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 10);
                CurveLabel.Draw(window.GraphicsDevice, window.Scene, window.spriteBatch, window.fontArial, window.curveManager, lineViews.LineLables.ToArray());
                ViewLabels.AppendLine("Chords");
            }

            if (unfiltered_lineViews != null && ShowAllEdges)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, unfiltered_lineViews.LineViews.ToArray());
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                CurveLabel.Draw(window.GraphicsDevice, window.Scene, window.spriteBatch, window.fontArial, window.curveManager, unfiltered_lineViews.LineLables.ToArray());
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                ViewLabels.AppendLine("Triangulation");
            }*/

            if (IncompletedVertexView != null && ShowCompletedVerticies)
            {
                IncompletedVertexView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);
                ViewLabels.AppendLine("Incomplete Verticies");
            }
            
            if (MeshVertsView != null && (this.VertexLabelType & IndexLabelType.MESH) > 0)
            {
                MeshVertsView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);
                ViewLabels.AppendLine("Mesh verticies");
            }
            
            if (RegionPolygonViews != null && ShowRegionPolygons)
            {
                
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, RegionPolygonViews.SelectMany(rpv => rpv.LineViews).ToArray());
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, RegionLabelViews);
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                ViewLabels.AppendLine("Region Polygon Views");
            }

            if (OTVTableView != null)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, OTVTableView.ToArray());
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                ViewLabels.AppendLine("OTV Table");
            }

            if (this.PolyViews != null && !ShowRegionPolygons &&  ((this.VertexLabelType & IndexLabelType.MESH) == 0))
            {
                DeviceStateManager.SetDepthStencilValue(window.GraphicsDevice, window.GraphicsDevice.DepthStencilState.ReferenceStencil + 1);
                PolyViews.Draw(window, scene);
                ViewLabels.AppendLine("Poly Views");
            }

            LabelView label = new LabelView(ViewLabels.ToString(), scene.VisibleWorldBounds.UpperLeft, anchor: Anchor.BottomLeft, scaleFontWithScene: false);
            LabelView.Draw(window.spriteBatch, window.fontArial, scene, new LabelView[] { label }); 
        }
        /*
        /// <summary>
        /// Dequeues entries from the CompletedMeshes
        /// </summary>
        private void MeshCompositeTask()
        {
            while(true)
            {
                bool NewMesh = false;
                while(CompletedMeshes.TryDequeue(out BajajGeneratorMesh completedMesh))
                {
                    //CompositeMeshModel.AddSlice(completedMesh)
                    //System.Threading.Thread.Sleep(1000); 
                    //var leaf = meshAssemblyPlan.Slices[completedMesh.Slice.Key];

                    NewMesh = true;
                }

                if (NewMesh)
                {
                    lock (drawlock)
                    { 
                        CompositeMeshView.models.Clear();

                        foreach (var model in meshAssemblyPlan.MeshModels)
                        {
                            CompositeMeshView.models.Add(model);
                        }
                    }
                }

                System.Threading.Thread.Sleep(100); //Consume all of the objects in the queue every interval
            }
        }*/

        public void Draw3D(MonoTestbed window, Scene3D scene)
        {

            DepthStencilState dstate = new DepthStencilState
            {
                DepthBufferEnable = true,
                StencilEnable = false,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual
            };

            window.GraphicsDevice.DepthStencilState = dstate;
            //window.GraphicsDevice.BlendState = BlendState.Opaque;

            //Expand our model if we can
            lock (drawlock)
            {
                if (ShowCompositeMesh == false)
                {
                    if (iShownMesh.HasValue && iShownMesh.Value < MeshViews.Count)
                    {
                        MeshViews[iShownMesh.Value].Draw(window.GraphicsDevice, scene, CullMode);
                    }
                }
                else
                {
                    if (CompositeMeshView != null)
                    {
                        try
                        {
                            //CompositeMeshModel.ModelLock.EnterReadLock();
                            //CompositeMeshView.Draw(window.GraphicsDevice, scene, CullMode);
                            //MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene, window.basicEffect, CullMode, meshAssemblyPlan.MeshModels);
                            if(meshIncompleteView != null)
                                MeshView<VertexPositionColor>.Draw(window.GraphicsDevice, scene, window.basicEffect, CullMode, FillMode.WireFrame, meshIncompleteView.MeshModels);
                            
                            if(meshCompletedView != null)
                                MeshView<VertexPositionNormalColor>.Draw(window.GraphicsDevice, scene, window.basicEffect, CullMode, FillMode.Solid, meshCompletedView.MeshModels);
                        }
                        finally
                        {
                            //CompositeMeshModel.ModelLock.ExitReadLock();
                        }
                        
                        //ViewLabels.AppendLine(CompositeMeshView.Name);
                    }
                }
            }
        }
    }
    

    /// <summary>
    /// Generates a single mesh for a cell or a subset of a cell based on a Z range.  Used to debug the generation of whole cells and the merging of multiple slice meshes.
    /// </summary>
    class BajajMultiAssignmentTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        
        Scene scene;
        Scene3D scene3D;
        readonly GamePadStateTracker Gamepad = new GamePadStateTracker();
        readonly KeyboardStateTracker keyboard = new KeyboardStateTracker();

        AnnotationVizLib.MorphologyGraph graph;

        //GridPolygon A;
        //GridPolygon B;

        readonly PointSetViewCollection Points_A = new PointSetViewCollection(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        readonly PointSetViewCollection Points_B = new PointSetViewCollection(Color.Red, Color.Pink, Color.Plum);
        readonly Cursor2DCameraManipulator CameraManipulator = new Cursor2DCameraManipulator();
        readonly Camera3DManipulator Camera3DManipulator = new Camera3DManipulator();
        readonly List<BajajMultiOTVAssignmentView> wrapViews = new List<BajajMultiOTVAssignmentView>();
        readonly bool Draw3D = true;

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }
         
        public async Task Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            this.scene3D = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D())
            {
                MaxDrawDistance = 1000000,
                MinDrawDistance = 1
            };

            Gamepad.Update(GamePad.GetState(PlayerIndex.One));
            keyboard.Update(Keyboard.GetState());

            Console.Write("Begin OData fetch");

            if (Program.options.StructureIDs.Count > 0 && Program.options.EndpointUri != null)
            {
                Console.WriteLine(" From command line parameters");

                Uri endpoint = Program.options.EndpointUri;
                graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(Program.options.StructureIDs, false, endpoint);

            }
            else
            {
                Console.WriteLine("From hard coded test case (no command line paramters)");

                //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(GlialDebug1, DataSource.EndpointMap[ENDPOINT.RPC1]);

                //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { 180 }, false, DataSource.EndpointMap[ENDPOINT.RC1]);
                //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { 40429 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);

                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 822, 23082, 23084 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);

                //Becca's paper, first render
                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 822, 2386, 23084, 23098, 31097, 31108, 23093 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);

                //Becca's paper, 2nd render
                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] {933, 23122, 31687, 23095, 23017, 23856, 39762 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);

                graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 476 }, false, DataSource.EndpointMap[Endpoint.TEST]);

                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 30804, 2713 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 933 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 933 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 23082 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 1161 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 1537 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
                //graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new ulong[] { 30804 }, false, DataSource.EndpointMap[ENDPOINT.RPC1]);
            }

            Console.WriteLine("End OData fetch");

            //graph = graph.Subgraphs.Values.First();


            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(BasicBranchInteriorHole, DataSource.EndpointMap[ENDPOINT.RPC1]);
            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(BasicBranchTroubleIDS, DataSource.EndpointMap[ENDPOINT.RPC1]);

            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(BasicInteriorHoleOverAdjacentExteriorRing, DataSource.EndpointMap[ENDPOINT.RPC1]);
            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(HorseshoeInteriorHoleOverAdjacentExteriorRing, DataSource.EndpointMap[ENDPOINT.RPC1]);

            /////////////
            ///This is the major test of mesh generation that covers as many cases as I could think of
            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(NightmareTroubleIDS, DataSource.EndpointMap[ENDPOINT.TEST]);
            //////////////

            //BajajMeshGenerator.ConvertToMeshGraph(graph);

            /*
            double MaxZ = 750;//graph.Nodes.Values.Max(n => n.Z);
            double MinZ = 500;//graph.Nodes.Values.Min(n => n.Z);

            Debug.Assert(MaxZ > MinZ);

            MaxZ = MaxZ * graph.scale.Z.Value;
            MinZ = MinZ * graph.scale.Z.Value;

            foreach ( var subgraph in graph.Subgraphs.Values)
            {
                foreach (var node in subgraph.Nodes.Values.ToList())
                {
                    if (node.Z < MinZ || node.Z > MaxZ)
                    {
                        subgraph.RemoveNode(node.ID);
                    }
                }
            }
            */

            if (window.Scene.RestoreCamera(TestMode.BAJAJTEST) == false)
            {
                window.Scene.Camera.LookAt = graph.BoundingBox.CenterPoint.XY().ToXNAVector2();
                window.Scene.Camera.Downsample = graph.BoundingBox.Width / (double)window.GraphicsDevice.Viewport.Width;
            }
            
            GridBox bbox = graph.BoundingBox;//new GridBox(wrapView.Polygons.BoundingBox(), nodes.Min(n => n.Z), nodes.Max(n => n.Z));
            scene3D.Camera.Position = (bbox.CenterPoint - new GridVector3(bbox.Width * 3, 0, 0)).ToXNAVector3();
            scene3D.Camera.LookAt = (bbox.CenterPoint).ToXNAVector3();

            var meshGenTasks = new List<Task>();
            //AnnotationVizLib.MorphologyNode[] nodes = graph.Nodes.Values.ToArray();
            foreach (var subgraph in graph.Subgraphs.Values)
            {
                var wrapView = new MonogameTestbed.BajajMultiOTVAssignmentView(subgraph);// (nodes.Select(n => n.Geometry.ToPolygon()).ToArray(), nodes.Select(n=> n.Z).ToArray());

                wrapViews.Add(wrapView);

                var task = wrapView.GenerateMesh();
                meshGenTasks.Add(task);
            }

            foreach(var t in meshGenTasks)
            {
                await t;
            }

            if(string.IsNullOrWhiteSpace(Program.options.OutputPath) == false)
            {

            }
            /*
            A = SqlGeometry.STPolyFromText(PolyA.ToSqlChars(), 0).ToPolygon();
            B = SqlGeometry.STPolyFromText(PolyB.ToSqlChars(), 0).ToPolygon();

            GridVector2 Centroid = A.Centroid;
            A = A.Translate(-Centroid);
            B = B.Translate(-Centroid);

            Points_A.Points = new MonogameTestbed.PointSet(A.ExteriorRing);
            Points_B.Points = new MonogameTestbed.PointSet(B.ExteriorRing);

            wrapView = new TriangulationShapeWrapView(A, B);
            */
        }


        public void Update()
        {
            PlayerIndex? InputSource = GamePadStateTracker.GetFirstConnectedController();
            if (InputSource == null)
                InputSource = PlayerIndex.One;

            GamePadState state = GamePad.GetState(InputSource.Value);
            Gamepad.Update(state);
            keyboard.Update(Keyboard.GetState());

            if (!Draw3D)
                CameraManipulator.Update(scene.Camera);
            else
            {
                Camera3DManipulator.Update(this.scene3D.Camera);
                //StandardCameraManipulator.Update(this.scene3D.Camera);
            }

            foreach (var wrapView in wrapViews)
            {

                if (Gamepad.A_Clicked)
                {
                    wrapView.iShownMesh = wrapView.iShownMesh.HasValue ? wrapView.iShownMesh.Value + 1 : 0;
                    if (wrapView.iShownMesh.HasValue && wrapView.iShownMesh.Value >= wrapView.MeshViews.Count)
                    {
                        wrapView.iShownMesh = null;
                    }
                }

                if (Gamepad.B_Clicked)
                {
                    wrapView.iShownLineView = wrapView.iShownLineView.HasValue ? wrapView.iShownLineView.Value + 1 : 0;
                    if (wrapView.iShownLineView.HasValue && wrapView.iShownLineView.Value >= wrapView.listLineViews.Count)
                    {
                        wrapView.iShownLineView = null;
                    }

                    Trace.WriteLine(wrapView.iShownLineView.ToString());

                    /*wrapView.ShowPolygons = !wrapView.ShowPolygons;
                    wrapView.ShowAllEdges = !wrapView.ShowAllEdges;
                    */
                }

                if (Gamepad.Y_Clicked)
                {
                    //Cycle throught the various region passes as Y is clicked
                    wrapView.iShownRegion = wrapView.iShownRegion.HasValue ? wrapView.iShownRegion.Value + 1 : 0;
                    if (wrapView.iShownRegion.HasValue && wrapView.iShownRegion.Value >= wrapView.RegionViews.Count)
                    {
                        wrapView.iShownRegion = null;
                    } 
                } 

                if (Gamepad.X_Clicked)
                {
                    wrapView.ShowCompletedVerticies = !wrapView.ShowCompletedVerticies;
                }

                if (Gamepad.Start_Clicked)
                {
                    //Recalculate the mesh from scratch
                    wrapView.GenerateMesh();
                }

                if (Gamepad.RightShoulder_Clicked)
                {
                    if ((wrapView.VertexLabelType & (IndexLabelType.MESH | IndexLabelType.POLYGON)) == 0)
                    {
                        wrapView.VertexLabelType = wrapView.VertexLabelType | IndexLabelType.MESH;
                    }
                    else if ((wrapView.VertexLabelType & IndexLabelType.POLYGON) > 0)
                    {
                        wrapView.VertexLabelType = IndexLabelType.NONE;
                    }
                    else if ((wrapView.VertexLabelType & IndexLabelType.MESH) == 0)
                    {
                        wrapView.VertexLabelType = wrapView.VertexLabelType | IndexLabelType.MESH;
                        wrapView.VertexLabelType = wrapView.VertexLabelType ^ IndexLabelType.POLYGON;
                    }
                    else if ((wrapView.VertexLabelType & IndexLabelType.POLYGON) == 0)
                    {
                        wrapView.VertexLabelType = wrapView.VertexLabelType | IndexLabelType.POLYGON;
                        wrapView.VertexLabelType = wrapView.VertexLabelType ^ IndexLabelType.MESH;
                    }
                }
                /*
                if(Gamepad.RightStick_Clicked)
                {
                    wrapView.VertexLabelType = wrapView.VertexLabelType ^ IndexLabelType.POSITION;
                }*/

                if (Gamepad.LeftStick_Clicked)
                {
                    wrapView.CullMode = wrapView.CullMode == CullMode.None ? CullMode.CullCounterClockwiseFace : CullMode.None;
                }

                if (Gamepad.LeftShoulder_Clicked)
                {
                    //this.Draw3D = !this.Draw3D;
                    wrapView.ShowCompositeMesh = !wrapView.ShowCompositeMesh;
                }

                if (Gamepad.Back_Clicked || keyboard.Pressed(Keys.PrintScreen))
                {
                    if(wrapView.meshAssemblyPlan != null && wrapView.meshAssemblyPlan.MeshAssembledEvent.IsSet)
                        SaveMesh(wrapView.meshAssemblyPlan.Root.MeshModel.composite, wrapView.Graph.BoundingBox.CenterPoint, wrapView.Graph.StructureID);
                }
            }

            if (Gamepad.Back_Clicked || (keyboard.Pressed(Keys.S) && (keyboard.Pressed(Keys.LeftControl) || keyboard.Pressed(Keys.RightControl))))
            {
                string outputPath = System.IO.Path.Combine(new string[] { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Morphology", $"BajajMultitest.dae" });
                //if (wrapView.meshAssemblyPlan.MeshAssembledEvent.IsSet)
                //SaveMesh(wrapView.meshAssemblyPlan.Root.MeshModel.composite, wrapView.Graph.StructureID);
                SaveMeshes("BajajMultitest", outputPath);
            }
            /*
            if(Gamepad.RightShoulder_Clicked)
            {
                wrapView.NumLinesToDraw++;
            }

            if (Gamepad.LeftShoulder_Clicked)
            {
                wrapView.NumLinesToDraw--;
            }

            if (Gamepad.Y_Clicked)
            {
                wrapView.ShowFinalLines = !wrapView.ShowFinalLines;
            }*/
        }

        public void Draw(MonoTestbed window)
        {
            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Color.DarkGray, float.MaxValue, 0);

            foreach (var wrapView in wrapViews)
            {
                if (!Draw3D)
                {
                    if (wrapView != null)
                        wrapView.Draw(window, scene);
                }
                else
                {
                    if (wrapView != null)
                        wrapView.Draw3D(window, scene3D);
                }
            }
        }

        public void UnloadContent(MonoTestbed window)
        {
            foreach (var wrapView in wrapViews)
            {
                if (wrapView != null)
                    wrapView.OnUnloadContent();
            }

            if(window.Scene != null)
                window.Scene.SaveCamera(TestMode.BAJAJTEST);
        }

        private string CleanOutputPath(string outputPath)
        {
            //System.IO.P
            throw new NotImplementedException();
        }

        public void SaveMeshes(string title, string outputPath)
        {
            BasicColladaView ColladaView = new BasicColladaView(graph.scale.X, null)
            {
                SceneTitle = title
            }; 

            foreach (var view in wrapViews)
            {
                if (view.meshAssemblyPlan == null || view.Graph == null)
                    continue;

                ulong structure_id = view.Graph.StructureID;
                if (view.meshAssemblyPlan.Root.MeshModel != null)
                {
                    var mesh = view.meshAssemblyPlan.Root.MeshModel.composite;
                    StructureModel rootModel = new StructureModel(structure_id, mesh,
                    new MaterialLighting(MaterialLighting.CreateKey(COLORSOURCE.STRUCTURE, graph.Subgraphs[structure_id].structure), System.Drawing.Color.CornflowerBlue))
                    {
                        Translation = view.Graph.BoundingBox.CenterPoint * 0.001
                    };

                    ColladaView.Add(rootModel);
                }
            }
 
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));

            DynamicRenderMeshColladaSerializer.SerializeToFile(ColladaView, outputPath);
        }

        public void SaveMesh(IReadOnlyMesh3D<IVertex3D> mesh, GridVector3 Position, ulong structure_id)
        {
            BasicColladaView ColladaView = new BasicColladaView(graph.scale.X, null)
            {
                SceneTitle = "BajajMultitest"
            };

            StructureModel rootModel = new StructureModel(structure_id, mesh,
                new MaterialLighting(MaterialLighting.CreateKey(COLORSOURCE.STRUCTURE, graph.Subgraphs[structure_id].structure), System.Drawing.Color.CornflowerBlue))
            {
                Translation = Position * 0.001
            };

            ColladaView.Add(rootModel);

            string outputPath = System.IO.Path.Combine(new string[] { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Morphology", $"Morphology-{structure_id}.dae" });
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));

            DynamicRenderMeshColladaSerializer.SerializeToFile(ColladaView, outputPath);
        }
    }
}
