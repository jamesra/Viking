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
    class MeshTest : IGraphicsTest
    {
        VikingXNAGraphics.MeshView<VertexPositionColor> meshView;        

        DynamicRenderMesh mesh = new DynamicRenderMesh();

        Scene3D Scene;

        LabelView labelCamera;

        private Vertex[] CreateTetrahedronVerts(GridVector3 offset)
        {
            Vertex[] verts = new Vertex[] {new Vertex(new GridVector3(0, 0, 0), new GridVector3(0, 0, 0)),
                                     new Vertex(new GridVector3(0, 1, 0), new GridVector3(0, 1, 0)),
                                     new Vertex(new GridVector3(0, 0, 1), new GridVector3(0, 0, 1)),
                                     new Vertex(new GridVector3(1, 0, 0), new GridVector3(1, 0, 0)) };

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

        public void Init(MonoTestbed window)
        {
            this.Scene = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D());
            this.meshView = new MeshView<VertexPositionColor>();

            this.Scene.Camera.LookAt = Vector3.Zero;
            this.Scene.Camera.Position = new Vector3(0, 10, 0);
             
            mesh.AddVertex(CreateTetrahedronVerts(new GridVector3(0,0,0)));
            Face[] faces = CreateTetrahedronFaces();
            foreach(Face f in faces)
            {
                mesh.AddFace(f);
            }

            Color[] colors = new Color[] { Color.Red, Color.Blue, Color.Green, Color.Yellow };

            MeshModel<VertexPositionColor> model = new MeshModel<VertexPositionColor>();
            model.Verticies = mesh.Verticies.Select((v, i) => new VertexPositionColor(v.Position.ToXNAVector3(), colors[i])).ToArray();
            model.Edges = mesh.Faces.SelectMany(f => f.iVerts).ToArray();

            meshView.models.Add(model);

            labelCamera = new LabelView("", new GridVector2(0, 50));
        } 

        public void Update()
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);

            if(state.ThumbSticks.Right.Y != 0)
                this.Scene.Camera.Pitch += state.ThumbSticks.Right.Y / (Math.PI * 2);
            if (state.ThumbSticks.Right.X != 0)
                this.Scene.Camera.Yaw -= state.ThumbSticks.Right.X / (Math.PI * 2);

            if (state.ThumbSticks.Left.Y != 0 || state.ThumbSticks.Left.X != 0 ||
                state.Triggers.Left != 0 || state.Triggers.Right != 0)
            {
                Vector3 translated = Scene.Camera.View.TranslateRelativeToViewMatrix(state.ThumbSticks.Left.X,
                                                                                     state.ThumbSticks.Left.Y,
                                                                                     state.Triggers.Right - state.Triggers.Left);
                this.Scene.Camera.Position += translated;
            }

            if (state.DPad.Left == ButtonState.Pressed)
            {
                Scene.Camera.Position = new Vector3(-10, 0, 0);
            }
            else if (state.DPad.Right == ButtonState.Pressed)
            {
                Scene.Camera.Position = new Vector3(10, 0, 0);
            }
            else if (state.DPad.Up == ButtonState.Pressed)
            {
                Scene.Camera.Position = new Vector3(0, -10,  0);
            }
            else if (state.DPad.Down == ButtonState.Pressed)
            {
                Scene.Camera.Position = new Vector3(0, 10, 0);
            }
            else if(state.Buttons.B == ButtonState.Pressed)
            {
                Scene.Camera.Position = new Vector3(0, 0, -10);
            }
            else if (state.Buttons.X == ButtonState.Pressed)
            {
                Scene.Camera.Position = new Vector3(0, 0, 10);
            }


            if (state.Buttons.Y == ButtonState.Pressed)
            {
                meshView.WireFrame = !meshView.WireFrame;
            }

            if (state.Buttons.A == ButtonState.Pressed)
            {
                this.Scene.Camera.Rotation = Vector3.Zero;
                this.Scene.Camera.Position = new Vector3(0, -10, 0);
            }

            labelCamera.Text = string.Format("{0} {1} {2}", Scene.Camera.Position, Scene.Camera.LookAt, Scene.Camera.Rotation);
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
