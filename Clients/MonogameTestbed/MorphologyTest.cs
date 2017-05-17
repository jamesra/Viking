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

namespace MonogameTestbed
{
    class MorphologyTest : IGraphicsTest
    {
        VikingXNAGraphics.MeshView<VertexPositionNormalColor> meshView;

        ICollection<DynamicRenderMesh<ulong>> meshes;

        Scene3D Scene;

        LabelView labelCamera;


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            this.Scene = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D());
            this.Scene.MaxDrawDistance = 1000000;
            this.Scene.MinDrawDistance = 1;
            this.meshView = new MeshView<VertexPositionNormalColor>();
             
            labelCamera = new LabelView("", new GridVector2(0, 100));

            //meshes = InitSmallSmoothModelFromOData(8883);
            //meshes = InitSmallSmoothModelFromOData(1);
            meshes = InitSmallSmoothModelFromOData(180);
            //meshes = InitSmallModelFromOData(476);
            //meshes = InitSmallModelFromOData(1);

            this.Scene.Camera.Position = (meshes.First().BoundingBox.CenterPoint * 0.9).ToXNAVector3();
            this.Scene.Camera.LookAt = Vector3.Zero;

            this.Scene.Camera.Position = new Vector3(120429, 111534, -20798);
            this.Scene.Camera.Rotation = new Vector3(4.986171f, 1.67181f, 0);
            //this.Scene.Camera.LookAt = meshes.First().BoundingBox.CenterPoint.ToXNAVector3();

            foreach (DynamicRenderMesh<ulong> mesh in meshes)
            {
                meshView.models.Add(mesh.ToVertexPositionNormalColorMeshModel(Color.Purple));
            }
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public ICollection<DynamicRenderMesh<ulong>> InitSmallTopologyModelFromOData(int CellID)
        {
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { CellID }, true, new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData"));
            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { CellID }, true, new Uri("http://websvc1.connectomes.utah.edu/RC2/OData"));

            MorphologyMesh.TopologyMeshGenerator generator = new MorphologyMesh.TopologyMeshGenerator();
            return generator.Generate(graph.Subgraphs.Values.First()); 
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public ICollection<DynamicRenderMesh<ulong>> InitSmallSmoothModelFromOData(int CellID)
        {
            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { CellID }, true, new Uri("http://webdev.connectomes.utah.edu/RC1Test/OData"));
            //AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { CellID }, true, new Uri("http://websvc1.connectomes.utah.edu/RC2/OData"));
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.SimpleOData.SimpleODataMorphologyFactory.FromOData(new long[] { CellID }, true, new Uri("http://websvc1.connectomes.utah.edu/RC1/OData"));

            //MorphologyMesh.TopologyMeshGenerator generator = new MorphologyMesh.TopologyMeshGenerator();
            return MorphologyMesh.SmoothMeshGenerator.Generate(graph.Subgraphs.Values.First());
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
