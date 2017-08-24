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
using AnnotationVizLib.SimpleOData;

namespace MonogameTestbed
{
    class MorphologyTest : IGraphicsTest
    {
        GamePadStateTracker Gamepad = new GamePadStateTracker();

        VikingXNAGraphics.MeshView<VertexPositionNormalColor> meshView;

        ICollection<DynamicRenderMesh<ulong>> meshes;

        Scene3D Scene;

        LabelView labelCamera;

        public enum ENDPOINT
        {
            TEST,
            RC1,
            RC2,
            TEMPORALMONKEY,
            INFERIORMONKEY
        }


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        private static Dictionary<ENDPOINT, Uri> EndpointMap = new Dictionary<ENDPOINT, Uri> { { ENDPOINT.TEST, new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData") },
                                                                                               { ENDPOINT.RC1, new Uri("http://websvc1.connectomes.utah.edu/RC1/OData") },
                                                                                               { ENDPOINT.RC2, new Uri("http://websvc1.connectomes.utah.edu/RC2/OData") },
                                                                                               { ENDPOINT.TEMPORALMONKEY, new Uri("http://websvc1.connectomes.utah.edu/NeitzTemporalMonkey/OData") },
                                                                                               { ENDPOINT.INFERIORMONKEY, new Uri("http://websvc1.connectomes.utah.edu/NeitzInferiorMonkey/OData") }};

        long[] TroubleIDS = new long[] {
            //5868, //Z: 231
            //5872, //Z: 232
//            6085,   //Z: 235
            //1333634, //Z: 235
            1026126, //Z: 234
            1026127, //Z: 233
            //1026128,  //Z: 232
            //1026129, //Z: 231
            };
                             

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            this.Scene = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D());
            this.Scene.MaxDrawDistance = 1000000;
            this.Scene.MinDrawDistance = 1;
            this.meshView = new MeshView<VertexPositionNormalColor>();
             
            labelCamera = new LabelView("", new GridVector2(0, 100));

            //meshes = InitSmallSmoothModelFromOData(144287, ENDPOINT.TEST);
            //meshes = InitSmallSmoothModelFromOData(1, ENDPOINT.RC2);
            //meshes = InitSmallSmoothModelFromOData(476, ENDPOINT.RC1);
            //meshes = InitSmallSmoothModelFromOData(476, ENDPOINT.RC1);
            //meshes = InitSmallSmoothModelFromOData(5554, ENDPOINT.RC2);
            meshes = InitSmallSmoothModelFromOData(new long[] { 1650, 858 }, ENDPOINT.INFERIORMONKEY);

            //Bad polygon Location #1026126.  Position X: 62134.0	Y: 51034.8	Z: 234	DS: 1.97
            //meshes = InitSmallSmoothModelFromOData(180, ENDPOINT.TEST);
            //            meshes = InitSmallSmoothModelFromODataLocations(TroubleIDS, ENDPOINT.TEST);

            //meshes = InitSmallSmoothModelFromOData(207, ENDPOINT.TEMPORALMONKEY);
            //meshes = InitSmallModelFromOData(476);
            //meshes = InitSmallModelFromOData(1);

            GridBox bbox = meshes.First().BoundingBox;

            this.Scene.Camera.Position = (bbox.CenterPoint - new GridVector3(bbox.Width / 2.0, bbox.Height / 2.0, 0)).ToXNAVector3();
            //this.Scene.Camera.Position = (bbox.CenterPoint * 0.9).ToXNAVector3();
            //this.Scene.Camera.Position = new Vector3(this.Scene.Camera.Position.X, this.Scene.Camera.Position.Y, -this.Scene.Camera.Position.Z);
            //            this.Scene.Camera.Position = bbox.CenterPoint.ToXNAVector3();
            this.Scene.Camera.LookAt = Vector3.Zero;

            //this.Scene.Camera.Position += new Vector3((float)bbox.Width, (float)bbox.Height, (float)bbox.Depth);
            //this.Scene.Camera.Rotation = new Vector3(4.986171f, 1.67181f, 0);
            //this.Scene.Camera.LookAt = meshes.First().BoundingBox.CenterPoint.ToXNAVector3();            
             
            System.Random r = new Random();
            foreach (DynamicRenderMesh<ulong> mesh in meshes)
            {
                meshView.models.Add(mesh.ToVertexPositionNormalColorMeshModel(new Color((float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble())));
            }
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public ICollection<DynamicRenderMesh<ulong>> InitSmallTopologyModelFromOData(int CellID, ENDPOINT endpoint)
        {
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { CellID }, true, EndpointMap[endpoint]); 

            MorphologyMesh.TopologyMeshGenerator generator = new MorphologyMesh.TopologyMeshGenerator();
            return generator.Generate(graph.Subgraphs.Values.First()); 
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public ICollection<DynamicRenderMesh<ulong>> InitSmallSmoothModelFromOData(long[] CellIDs, ENDPOINT endpoint)
        { 
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(CellIDs, true, EndpointMap[endpoint]);

            //SelectZRange(graph, 231, 235);
            //SelectSubsetOfIDs(graph, TroubleIDS);

            //MorphologyMesh.TopologyMeshGenerator generator = new MorphologyMesh.TopologyMeshGenerator();
            return RecursivelyGenerateMeshes(graph);
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public ICollection<DynamicRenderMesh<ulong>> InitSmallSmoothModelFromODataLocations(long[] LocationIDs, ENDPOINT endpoint)
        {
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromODataLocationIDs(LocationIDs, EndpointMap[endpoint]);
             
            //MorphologyMesh.TopologyMeshGenerator generator = new MorphologyMesh.TopologyMeshGenerator();
            return RecursivelyGenerateMeshes(graph);
        }
         
        private void SelectZRange(AnnotationVizLib.MorphologyGraph graph, int StartZ, int EndZ)
        {
            AnnotationVizLib.MorphologyNode[] nodes = graph.Nodes.Values.Where(n => n.UnscaledZ < StartZ || n.UnscaledZ > EndZ).ToArray();
            
            for(int i = 0; i < nodes.Length; i++)
            {
                graph.RemoveNode(nodes[i].Key);
            }

            foreach(AnnotationVizLib.MorphologyGraph subgraph in graph.Subgraphs.Values)
            {
                SelectZRange(subgraph, StartZ, EndZ);
            }

            return;
        }

        private void SelectSubsetOfIDs(AnnotationVizLib.MorphologyGraph graph, long[] IDs)
        {
            AnnotationVizLib.MorphologyNode[] nodes = graph.Nodes.Values.Where(n => !IDs.Contains((long)n.Key)).ToArray();

            for (int i = 0; i < nodes.Length; i++)
            {
                graph.RemoveNode(nodes[i].Key);
            }

            foreach (AnnotationVizLib.MorphologyGraph subgraph in graph.Subgraphs.Values)
            {
                SelectSubsetOfIDs(subgraph, IDs); 
            }
            
            return;
        }


        private ICollection<DynamicRenderMesh<ulong>> RecursivelyGenerateMeshes(AnnotationVizLib.MorphologyGraph graph)
        {
            List<DynamicRenderMesh<ulong>> listMeshes = new List<DynamicRenderMesh<ulong>>();

            DynamicRenderMesh<ulong> structureMesh = MorphologyMesh.SmoothMeshGenerator.Generate(graph);
            if(structureMesh != null)
                listMeshes.Add(structureMesh);

            foreach(var subgraph in graph.Subgraphs.Values)
            {
                listMeshes.AddRange( RecursivelyGenerateMeshes(subgraph) );
            }

            return listMeshes;
        }

        public void Update()
        {
            StandardCameraManipulator.Update(this.Scene.Camera);

            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state);

            if (Gamepad.Y_Clicked)
            {
                meshView.WireFrame = !meshView.WireFrame;
            }

            if (Gamepad.A_Clicked)
            {
                this.Scene.Camera.Rotation = Vector3.Zero;
                this.Scene.Camera.Position = new Vector3(0, -10, 0);
            }

            GridVector3 VolumePosition = Scene.Camera.Position.ToGridVector3();
            VolumePosition /= new GridVector3(2.18, 2.18, -90);

            labelCamera.Text = string.Format("{0}\n{1}\n{2}", Scene.Camera.Position, VolumePosition, Scene.Camera.Rotation);
              
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
            meshView.Draw(window.GraphicsDevice, this.Scene, CullMode.None); 

            
            window.spriteBatch.Begin();
            labelCamera.Draw(window.spriteBatch, window.fontArial, window.Scene);
            window.spriteBatch.End(); 
        }
    }
}
