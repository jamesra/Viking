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
    class Cursor2DCameraManipulator
    {
        DateTime? RightThumbstickStartTime = null;

        GamePadStateTracker Gamepad = null;

        public float CameraTranslateSensitivity = 3.0f;

        double RightThumbstickTimeScalar
        {
            get
            {
                if (!RightThumbstickStartTime.HasValue)
                    return 1.0;

                double elapsed = (DateTime.UtcNow - RightThumbstickStartTime.Value).Seconds;

                if(elapsed > 5.0)
                {
                    elapsed = 5.0;
                }
                else if(elapsed <= 1.0)
                {
                    elapsed = 1.0;
                }

                return elapsed;
            }
        }

        public void Update(Camera Camera)
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);

            if (Gamepad == null)
            {
                Gamepad = new MonogameTestbed.GamePadStateTracker();
                Gamepad.Update(state);
            }
            
            if (state.ThumbSticks.Right != Vector2.Zero)
            {
                if(!RightThumbstickStartTime.HasValue)
                {
                    RightThumbstickStartTime = DateTime.UtcNow;
                }

                Vector2 RightStick = state.ThumbSticks.Right;
                RightStick.X *= CameraTranslateSensitivity;
                RightStick.Y *= CameraTranslateSensitivity;
                Vector2 offset = new Vector2(RightStick.X * RightStick.X, RightStick.Y * RightStick.Y);

                offset.X = RightStick.X < 0 ? -offset.X : offset.X;
                offset.Y = RightStick.Y < 0 ? -offset.Y : offset.Y;
                
                double scalar = RightThumbstickTimeScalar;
                if(Camera.Downsample > 1.0)
                    scalar *= Camera.Downsample;

                offset = new Vector2((float)(offset.X * scalar), (float)(offset.Y * scalar));
                Camera.LookAt += offset;
            }
            else
            {
                RightThumbstickStartTime = null;
            }

            if (state.Triggers.Left > 0)
            {
                Camera.Downsample *= 1.0 - (state.Triggers.Left / 10);

                if (Camera.Downsample <= 0.1)
                {
                    Camera.Downsample = 0.1;
                }
            }

            if (state.Triggers.Right > 0)
            {
                Camera.Downsample *= 1.0 + (state.Triggers.Right / 10);

                if (Camera.Downsample >= 100)
                {
                    Camera.Downsample = 100;
                }
            }

            if (Gamepad.RightStick_Clicked)
            {
                Camera.Downsample = 1;
                Camera.LookAt = Vector2.Zero;
            }
        }
    }
}
