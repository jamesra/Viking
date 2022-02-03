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
    class PointPlacementTestTemplate : IGraphicsTest
    {
        VikingXNAGraphics.MeshView<VertexPositionNormalColor> meshView;
        Scene scene;
        List<GridCircle> Points_A = new List<GridCircle>();
        CircleView[] Views_A = new CircleView[0];
        List<GridCircle> Points_B = new List<GridCircle>();
        CircleView[] Views_B = new CircleView[0];
        GamePadStateTracker Gamepad = new GamePadStateTracker();

        GridVector2 Cursor;
        CircleView cursorView;

        static double PointRadius = 2.0;

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }
        
        public Task Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Gamepad.Update(GamePad.GetState(PlayerIndex.One));
        }

        public void Update()
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state);

            //StandardCameraManipulator.Update(this.Scene.Camera);

            if (state.ThumbSticks.Left != Vector2.Zero)
            {
                Cursor += state.ThumbSticks.Left.ToGridVector2();
                cursorView = new CircleView(new GridCircle(Cursor, PointRadius), Color.Gray);
            }

            if (state.ThumbSticks.Right != Vector2.Zero)
            {
                scene.Camera.LookAt += state.ThumbSticks.Right;
            }

            if(state.Triggers.Left > 0)
            {
                scene.Camera.Downsample *= 1.0 - (state.Triggers.Left / 10);

                if(scene.Camera.Downsample <= 0.1)
                {
                    scene.Camera.Downsample = 0.1;
                }
            }

            if (state.Triggers.Right > 0)
            {
                scene.Camera.Downsample *= 1.0 + (state.Triggers.Right / 10);

                if (scene.Camera.Downsample >= 100)
                {
                    scene.Camera.Downsample = 100;
                }
            }

            if(Gamepad.RightStick_Clicked)
            {
                scene.Camera.Downsample = 1;
                scene.Camera.LookAt = Vector2.Zero;
            }

            if (Gamepad.A_Clicked)
            {
                GridCircle newCircle = new GridCircle(Cursor, PointRadius);
                if(Points_A.Any(p => p.Intersects(newCircle)))
                {
                    Points_A.RemoveAll(c => c.Intersects(newCircle));
                }
                else
                {
                    Points_A.Add(newCircle);
                }

                Views_A = Points_A.Select(c => new CircleView(c, Color.Blue)).ToArray();
            }

            if (Gamepad.B_Clicked)
            {
                GridCircle newCircle = new GridCircle(Cursor, PointRadius);
                if (Points_B.Any(p => p.Intersects(newCircle)))
                {
                    Points_B.RemoveAll(c => c.Intersects(newCircle));
                }
                else
                {
                    Points_B.Add(newCircle);
                }

                Views_B = Points_B.Select(c => new CircleView(c, Color.Red)).ToArray();
            }
        }

        public void Draw(MonoTestbed window)
        {
            if(cursorView != null)
                CircleView.Draw(window.GraphicsDevice, this.scene, window.basicEffect, window.overlayEffect, new CircleView[] { cursorView });

            CircleView.Draw(window.GraphicsDevice, this.scene, window.basicEffect, window.overlayEffect, Views_A);
            CircleView.Draw(window.GraphicsDevice, this.scene, window.basicEffect, window.overlayEffect, Views_B);
        }
    }
}
