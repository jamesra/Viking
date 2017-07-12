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
        VikingXNAGraphics.MeshView<VertexPositionNormalColor> meshView;

        ICollection<DynamicRenderMesh<ulong>> meshes;

        Scene3D Scene;

        LabelView labelCamera;

        public enum ENDPOINT
        {
            TEST,
            RC1,
            RC2,
            TEMPORALMONKEY
        }


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        private static Dictionary<ENDPOINT, Uri> EndpointMap = new Dictionary<ENDPOINT, Uri> { { ENDPOINT.TEST, new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData") },
                                                                                               { ENDPOINT.RC1, new Uri("http://websvc1.connectomes.utah.edu/RC1/OData") },
                                                                                               { ENDPOINT.RC2, new Uri("http://websvc1.connectomes.utah.edu/RC2/OData") },
                                                                                               { ENDPOINT.TEMPORALMONKEY, new Uri("http://websvc1.connectomes.utah.edu/NeitzTemporalMonkey/OData") }};

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
            meshes = InitSmallSmoothModelFromOData(180, ENDPOINT.TEST);
            //meshes = InitSmallSmoothModelFromOData(207, ENDPOINT.TEMPORALMONKEY);
            //meshes = InitSmallModelFromOData(476);
            //meshes = InitSmallModelFromOData(1);

            GridBox bbox = meshes.First().BoundingBox;

            this.Scene.Camera.Position = (bbox.CenterPoint * 0.9).ToXNAVector3();
            this.Scene.Camera.LookAt = Vector3.Zero;

            this.Scene.Camera.Position += new Vector3((float)bbox.Width, (float)bbox.Height, (float)bbox.Depth);
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
        public ICollection<DynamicRenderMesh<ulong>> InitSmallSmoothModelFromOData(int CellID, ENDPOINT endpoint)
        { 
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { CellID }, true, EndpointMap[endpoint]);

            //MorphologyMesh.TopologyMeshGenerator generator = new MorphologyMesh.TopologyMeshGenerator();
            return RecursivelyGenerateMeshes(graph);
        }

        private ICollection<DynamicRenderMesh<ulong>> RecursivelyGenerateMeshes(AnnotationVizLib.MorphologyGraph graph)
        {
            List<DynamicRenderMesh<ulong>> listMeshes = new List<DynamicRenderMesh<ulong>>();

            listMeshes.AddRange(MorphologyMesh.SmoothMeshGenerator.Generate(graph));

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
            
            if (state.Buttons.Y == ButtonState.Pressed)
            {
                meshView.WireFrame = !meshView.WireFrame;
            }

            if (state.Buttons.A == ButtonState.Pressed)
            {
                this.Scene.Camera.Rotation = Vector3.Zero;
                this.Scene.Camera.Position = new Vector3(0, -10, 0);
            }

            labelCamera.Text = string.Format("{0}\n{1}\n{2}", Scene.Camera.Position, Scene.Camera.LookAt, Scene.Camera.Rotation);
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

            
            window.spriteBatch.Begin();
            labelCamera.Draw(window.spriteBatch, window.fontArial, window.Scene);
            window.spriteBatch.End(); 
        }



    }
}
