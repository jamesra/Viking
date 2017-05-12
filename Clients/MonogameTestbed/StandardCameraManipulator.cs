using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using VikingXNAGraphics;
using VikingXNA;

namespace MonogameTestbed
{
    static class StandardCameraManipulator
    {
        static DateTime? TriggerDownStartTime = new DateTime?();

        public static void Update(Camera3D Camera)
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);

            if (state.ThumbSticks.Right.Y != 0)
                Camera.Pitch += state.ThumbSticks.Right.Y / (Math.PI * 2);
            if (state.ThumbSticks.Right.X != 0)
                Camera.Yaw -= state.ThumbSticks.Right.X / (Math.PI * 2);

            if (state.ThumbSticks.Left.Y != 0 || state.ThumbSticks.Left.X != 0 ||
                state.Triggers.Left != 0 || state.Triggers.Right != 0)
            {
                double elapsed = 0;
                if (!TriggerDownStartTime.HasValue)
                {
                    TriggerDownStartTime = DateTime.UtcNow;
                }
                else
                {
                    TimeSpan elapsedTime = DateTime.UtcNow - TriggerDownStartTime.Value;
                    elapsed = elapsedTime.TotalSeconds;
                    if (elapsed < 1)
                        elapsed = 1;
                }


                Vector3 translated = Camera.View.TranslateRelativeToViewMatrix(state.ThumbSticks.Left.X,
                                                                               state.Triggers.Right - state.Triggers.Left,
                                                                               -state.ThumbSticks.Left.Y);

                float scalar = (float)Math.Pow(2, elapsed);
                translated = new Vector3(translated.X * scalar, translated.Y * scalar, translated.Z * scalar);

                Camera.Position += translated;
            }
            else
            {
                TriggerDownStartTime = new DateTime?();
            }

            if (state.DPad.Left == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(-10, 0, 0);
            }
            else if (state.DPad.Right == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(10, 0, 0);
            }
            else if (state.DPad.Up == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(0, -10, 0);
            }
            else if (state.DPad.Down == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(0, 10, 0);
            }
            else if (state.Buttons.B == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(0, 0, -10);
            }
            else if (state.Buttons.X == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(0, 0, 10);
            }

            if (state.Buttons.A == ButtonState.Pressed)
            {
                Camera.Rotation = Vector3.Zero;
                Camera.Position = new Vector3(0, -10, 0);
            }
        }
    }
}
