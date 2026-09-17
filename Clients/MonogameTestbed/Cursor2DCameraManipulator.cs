using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using VikingXNA;

namespace MonogameTestbed
{
    /// <summary>
    /// 2D LookAt / Downsample control for VikingXNA.Camera. Gamepad, mouse, and keyboard are parallel
    /// devices with the same roles: pan (right stick / right-drag / WASD), zoom (triggers / wheel / Q-E),
    /// reset (right-stick click / middle-click / Home). Called from BajajTest, BajajMultiTest, and other
    /// 2D IGraphicsTest Update loops.
    /// </summary>
    class Cursor2DCameraManipulator
    {
        DateTime? RightThumbstickStartTime = null;

        readonly GamePadStateTracker Gamepad = new();
        readonly MouseStateTracker Mouse = new();
        readonly KeyboardStateTracker Keyboard = new();

        public float CameraTranslateSensitivity = 3.0f;

        const double MinDownsample = 0.01;
        const double MaxDownsample = 100;

        double RightThumbstickTimeScalar
        {
            get
            {
                if (!RightThumbstickStartTime.HasValue)
                    return 1.0;

                double elapsed = (DateTime.UtcNow - RightThumbstickStartTime.Value).Seconds;

                if (elapsed > 5.0)
                    elapsed = 5.0;
                else if (elapsed <= 1.0)
                    elapsed = 1.0;

                return elapsed;
            }
        }

        public void Update(Camera Camera)
        {
            GamePadState pad = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(pad);
            Mouse.Update(Microsoft.Xna.Framework.Input.Mouse.GetState());
            Keyboard.Update(Microsoft.Xna.Framework.Input.Keyboard.GetState());

            UpdateFromGamepad(Camera, pad);
            UpdateFromMouse(Camera);
            UpdateFromKeyboard(Camera);
        }

        void UpdateFromGamepad(Camera Camera, GamePadState state)
        {
            if (state.ThumbSticks.Right != Vector2.Zero)
            {
                if (!RightThumbstickStartTime.HasValue)
                    RightThumbstickStartTime = DateTime.UtcNow;

                Vector2 RightStick = state.ThumbSticks.Right;
                RightStick.X *= CameraTranslateSensitivity;
                RightStick.Y *= CameraTranslateSensitivity;
                Vector2 offset = new(RightStick.X * RightStick.X, RightStick.Y * RightStick.Y);

                offset.X = RightStick.X < 0 ? -offset.X : offset.X;
                offset.Y = RightStick.Y < 0 ? -offset.Y : offset.Y;

                double scalar = RightThumbstickTimeScalar;
                if (Camera.Downsample > 1.0)
                    scalar *= Camera.Downsample;

                offset = new Vector2((float)(offset.X * scalar), (float)(offset.Y * scalar));
                Camera.LookAt += offset;
            }
            else
            {
                RightThumbstickStartTime = null;
            }

            if (state.Triggers.Left > 0)
                Zoom(Camera, 1.0 - (state.Triggers.Left / 10));

            if (state.Triggers.Right > 0)
                Zoom(Camera, 1.0 + (state.Triggers.Right / 10));

            if (Gamepad.RightStick_Clicked)
                Reset(Camera);
        }

        void UpdateFromMouse(Camera Camera)
        {
            if (Mouse.Down[MouseButton.Right] || Mouse.Clicked[MouseButton.Right])
            {
                double ds = Camera.Downsample;
                Camera.LookAt += new Vector2(
                    (float)(Mouse.PositionDelta.X * ds),
                    (float)(-Mouse.PositionDelta.Y * ds));
            }

            int wheel = Mouse.ScrollWheelValueDelta;
            if (wheel != 0)
                Zoom(Camera, 1.0 + (wheel / 1200.0));

            if (Mouse.Clicked[MouseButton.Middle])
                Reset(Camera);
        }

        void UpdateFromKeyboard(Camera Camera)
        {
            bool shift = PressedOrDown(Keys.LeftShift) || PressedOrDown(Keys.RightShift);
            float stepScalar = shift ? 10.0f : 1.0f;
            double ds = Math.Max(Camera.Downsample, 1.0);
            float pan = CameraTranslateSensitivity * stepScalar * (float)ds;

            Vector2 offset = Vector2.Zero;
            if (PressedOrDown(Keys.W))
                offset.Y += pan * HoldScalar(Keys.W);
            if (PressedOrDown(Keys.S))
                offset.Y -= pan * HoldScalar(Keys.S);
            if (PressedOrDown(Keys.A))
                offset.X -= pan * HoldScalar(Keys.A);
            if (PressedOrDown(Keys.D))
                offset.X += pan * HoldScalar(Keys.D);

            Camera.LookAt += offset;

            if (PressedOrDown(Keys.Q))
                Zoom(Camera, 1.0 + (0.05 * stepScalar * HoldScalar(Keys.Q)));
            if (PressedOrDown(Keys.E))
                Zoom(Camera, 1.0 - (0.05 * stepScalar * HoldScalar(Keys.E)));

            if (Keyboard.Pressed(Keys.Home))
                Reset(Camera);
        }

        bool PressedOrDown(Keys key) => Keyboard.Pressed(key) || Keyboard.Down(key);

        float HoldScalar(Keys key) => Camera3DManipulator.ScalarForElapsedTime(Keyboard.PressDuration(key).TotalSeconds);

        static void Zoom(Camera Camera, double factor)
        {
            Camera.Downsample *= factor;
            if (Camera.Downsample < MinDownsample)
                Camera.Downsample = MinDownsample;
            else if (Camera.Downsample > MaxDownsample)
                Camera.Downsample = MaxDownsample;
        }

        static void Reset(Camera Camera)
        {
            Camera.Downsample = 1;
            Camera.LookAt = Vector2.Zero;
        }
    }
}
