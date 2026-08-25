using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using VikingXNA;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    static class StandardCameraManipulator
    {
        static DateTime? TriggerDownStartTime = new DateTime?();

        /// <summary>
        /// Basic camera manipulation for only gamepad
        /// </summary>
        /// 
        /// <param name="Camera"></param>
        /// <param name="unitStepSize">How far to move the camera for a single d-pad click or input event</param>
        public static void Update(Camera3D Camera, float unitStepSize = 10.0f)
        {
            PlayerIndex? InputSource = GamePadStateTracker.GetFirstConnectedController() ?? PlayerIndex.One;
            GamePadState state = GamePad.GetState(InputSource.Value);


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
                Camera.Position = new Vector3(-unitStepSize, 0, 0);
            }
            else if (state.DPad.Right == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(unitStepSize, 0, 0);
            }
            else if (state.DPad.Up == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(0, -unitStepSize, 0);
            }
            else if (state.DPad.Down == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(0, unitStepSize, 0);
            }
            else if (state.Buttons.B == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(0, 0, -unitStepSize);
            }
            else if (state.Buttons.X == ButtonState.Pressed)
            {
                Camera.Position = new Vector3(0, 0, unitStepSize);
            }

            if (state.Buttons.RightStick == ButtonState.Pressed)
            {
                Camera.Rotation = Vector3.Zero;
                Camera.Position = new Vector3(0, -unitStepSize, 0);
            }
        }
    }

    /// <summary>
    /// Gamepad, mouse, and keyboard orbit/dolly for a <see cref="Camera3D"/>. BajajTest and BajajMultiTest
    /// call this when drawing 3D; other mesh tests still use gamepad-only <see cref="StandardCameraManipulator"/>.
    /// </summary>
    internal class Camera3DManipulator
    {
        readonly KeyboardStateTracker keyboard = new();
        readonly GamePadStateTracker gamepad = new();
        readonly MouseStateTracker mouse = new();

        public float UnitStepSize = 10.0f;
        const double OneDegree = (Math.PI * 2.0 / 360);
        public double PitchRawStepSize = OneDegree;

        /// <summary>
        /// Polls all three input devices and applies gamepad, WASD/E/C/PageUp keyboard, and mouse mappings.
        /// Viewport size scales right-drag so a full-width drag is about one turn (same as Jotunn Camera3DBehavior).
        /// </summary>
        public void Update(Camera3D Camera, int viewportWidth, int viewportHeight)
        {
            keyboard.Update(Keyboard.GetState());
            mouse.Update(Mouse.GetState());
            PlayerIndex? InputSource = GamePadStateTracker.GetFirstConnectedController() ?? PlayerIndex.One;
            GamePadState state = GamePad.GetState(InputSource.Value);
            gamepad.Update(state);

            UpdateCameraFromGamepad(Camera);
            UpdateCameraFromKeyboard(Camera);
            UpdateCameraFromMouse(Camera, viewportWidth, viewportHeight);
        }

        public void UpdateCameraFromGamepad(Camera3D camera) => StandardCameraManipulator.Update(camera, UnitStepSize);

        private bool PressedOrDown(Keys key) => keyboard.Pressed(key) || keyboard.Down(key);

        public void UpdateCameraFromKeyboard(Camera3D Camera)
        {
            Vector3 translation = new();
            bool CapsLockDown = PressedOrDown(Keys.CapsLock);
            bool ShiftDown = PressedOrDown(Keys.LeftShift) || PressedOrDown(Keys.RightShift);
            bool CtrlDown = PressedOrDown(Keys.LeftControl) || PressedOrDown(Keys.RightControl);

            float stepScalar = ShiftDown ? 10.0f : 1.0f;
            double rotateScalar = ShiftDown ? 5.0 : 1.0;

            if (PressedOrDown(Keys.W))
            {
                if (CtrlDown)
                {
                    Camera.Pitch -= OneDegree * rotateScalar;
                }
                else
                    translation += new Vector3(0, 0, -UnitStepSize * stepScalar) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.W) : 1);
            }
            if (PressedOrDown(Keys.S))
            {
                if (CtrlDown)
                {
                    Camera.Pitch += OneDegree * rotateScalar;
                }
                else
                    translation += new Vector3(0, 0, UnitStepSize * stepScalar) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.S) : 1);
            }
            if (PressedOrDown(Keys.A))
            {
                if (CtrlDown)
                {
                    Camera.Yaw += OneDegree * rotateScalar;
                }
                else
                    translation += new Vector3(-UnitStepSize * stepScalar, 0, 0) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.A) : 1);
            }
            if (PressedOrDown(Keys.D))
            {
                if (CtrlDown)
                {
                    Camera.Yaw -= OneDegree * rotateScalar;
                }
                else
                    translation += new Vector3(UnitStepSize * stepScalar, 0, 0) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.D) : 1);
            }
            if (PressedOrDown(Keys.E))
            {
                translation += new Vector3(0, UnitStepSize * stepScalar, 0) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.E) : 1);
            }
            if (PressedOrDown(Keys.C))
            {
                translation += new Vector3(0, -UnitStepSize * stepScalar, 0) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.C) : 1);
            }

            if (PressedOrDown(Keys.PageUp))
                Camera.Position += new Vector3(0, 0, UnitStepSize * stepScalar * (CapsLockDown ? ScalarForElapsedDownTime(Keys.PageUp) : 1));
            if (PressedOrDown(Keys.PageDown))
                Camera.Position += new Vector3(0, 0, -UnitStepSize * stepScalar * (CapsLockDown ? ScalarForElapsedDownTime(Keys.PageDown) : 1));

            Vector3 translated = Camera.View.TranslateRelativeToViewMatrix(translation.X, translation.Y, translation.Z);
            Camera.Position += translated;
        }

        /// <summary>
        /// Maps mouse onto the same 3D camera roles as the gamepad: left-drag translates in view XY
        /// (left stick), right-drag yaws/pitches (right stick), wheel dollies (triggers), middle-click
        /// resets (right-stick click). Right-drag uses viewport size so a full-width drag is about one turn.
        /// Called from <see cref="Update"/>.
        /// </summary>
        public void UpdateCameraFromMouse(Camera3D camera, int viewportWidth, int viewportHeight)
        {
            const float dragPixelsToStick = 200f;
            const float wheelTicks = 120f;
            float width = Math.Max(viewportWidth, 1);
            float height = Math.Max(viewportHeight, 1);

            if (mouse.Down[MouseButton.Left] || mouse.Clicked[MouseButton.Left])
            {
                Vector3 translated = camera.View.TranslateRelativeToViewMatrix(
                    mouse.PositionDelta.X / dragPixelsToStick * UnitStepSize,
                    -mouse.PositionDelta.Y / dragPixelsToStick * UnitStepSize,
                    0);
                camera.Position += translated;
            }

            if (mouse.Down[MouseButton.Right] || mouse.Clicked[MouseButton.Right])
            {
                camera.Yaw += mouse.PositionDelta.X / width * (Math.PI * 2);
                camera.Pitch += mouse.PositionDelta.Y / height * (Math.PI * 2);
            }

            int wheel = mouse.ScrollWheelValueDelta;
            if (wheel != 0)
            {
                Vector3 dolly = camera.View.TranslateRelativeToViewMatrix(0, 0, UnitStepSize * (wheel / wheelTicks));
                camera.Position += dolly;
            }

            if (mouse.Clicked[MouseButton.Middle])
            {
                camera.Rotation = Vector3.Zero;
                camera.Position = new Vector3(0, -UnitStepSize, 0);
            }
        }

        public float ScalarForElapsedDownTime(Keys key)
        {
            var elapsed = keyboard.PressDuration(key);
            return ScalarForElapsedTime(elapsed.TotalSeconds);
        }

        public float ScalarForElapsedTime(TimeSpan elapsed) => ScalarForElapsedTime(elapsed.TotalSeconds);

        public static float ScalarForElapsedTime(double elapsed)
        {
            if (elapsed < 1)
            {
                return 1;
            }

            float scalar = (float)Math.Pow(2, elapsed);
            return scalar;

        }
    }

}
