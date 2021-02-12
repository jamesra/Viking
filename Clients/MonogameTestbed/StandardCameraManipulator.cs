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

        /// <summary>
        /// Basic camera manipulation for only gamepad
        /// </summary>
        /// 
        /// <param name="Camera"></param>
        /// <param name="unitStepSize">How far to move the camera for a single d-pad click or input event</param>
        public static void Update(Camera3D Camera, float unitStepSize=10.0f)
        { 
            PlayerIndex? InputSource = GamePadStateTracker.GetFirstConnectedController();
            if (InputSource == null)
                InputSource = PlayerIndex.One;

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

    internal class Camera3DManipulator
    {
        KeyboardStateTracker keyboard = new KeyboardStateTracker();
        GamePadStateTracker gamepad = new GamePadStateTracker();
        MouseStateTracker mouse = new MouseStateTracker();

        public float UnitStepSize = 10.0f;
        const double OneDegree = (Math.PI * 2.0 / 360);
        public double PitchRawStepSize = OneDegree;

        public void Update(Camera3D Camera)
        {
            keyboard.Update(Keyboard.GetState());
            mouse.Update(Mouse.GetState());
            PlayerIndex? InputSource = GamePadStateTracker.GetFirstConnectedController();
            if (InputSource == null)
                InputSource = PlayerIndex.One;

            GamePadState state = GamePad.GetState(InputSource.Value);
            gamepad.Update(state);

            UpdateCameraFromGamepad(Camera);
            UpdateCameraFromKeyboard(Camera);
            UpdateCameraFromMouse(Camera);
        }

        public void UpdateCameraFromGamepad(Camera3D camera)
        {
            StandardCameraManipulator.Update(camera, UnitStepSize);
        }

        private bool PressedOrDown(Keys key)
        {
            return keyboard.Pressed(key) || keyboard.Down(key);
        }

        public void UpdateCameraFromKeyboard(Camera3D Camera)
        {
            Vector3 translation = new Vector3();
            bool CapsLockDown = PressedOrDown(Keys.CapsLock);
            bool ShiftDown = PressedOrDown(Keys.LeftShift) || PressedOrDown(Keys.RightShift);
            bool CtrlDown = PressedOrDown(Keys.LeftControl) || PressedOrDown(Keys.RightControl);

            if (PressedOrDown(Keys.W))
            {
                if(ShiftDown)
                {
                    Camera.Pitch -= OneDegree * (CtrlDown ? 5 : 1);
                }
                else 
                    translation += new Vector3(0, 0, -UnitStepSize) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.W) : 1);
            }
            if (PressedOrDown(Keys.S))
            {
                if (ShiftDown)
                {
                    Camera.Pitch += OneDegree * (CtrlDown ? 5 : 1);
                }
                else
                    translation += new Vector3(0, 0, UnitStepSize) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.S) : 1);
            }
            if (PressedOrDown(Keys.A))
            {
                if (ShiftDown)
                {
                    Camera.Yaw += OneDegree * (CtrlDown ? 5 : 1);
                }
                else
                    translation += new Vector3(-UnitStepSize, 0, 0) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.A) : 1);
            }
            if (PressedOrDown(Keys.D))
            {
                if (ShiftDown)
                {
                    Camera.Yaw -= OneDegree * (CtrlDown ? 5 : 1);
                }
                else
                    translation += new Vector3(UnitStepSize, 0, 0) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.D) : 1);
            }
            if (PressedOrDown(Keys.E))
            { 
                translation += new Vector3(0, UnitStepSize, 0) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.E) : 1);
            }
            if (PressedOrDown(Keys.C))
            {
                translation += new Vector3(0, -UnitStepSize, 0) * (CapsLockDown ? ScalarForElapsedDownTime(Keys.C) : 1);
            }
             

            Vector3 translated = Camera.View.TranslateRelativeToViewMatrix(translation.X, translation.Y, translation.Z);
            Camera.Position += translated;
        }

        public void UpdateCameraFromMouse(Camera3D camera)
        { 
        }

        public float ScalarForElapsedDownTime(Keys key)
        {
            var elapsed = keyboard.PressDuration(key);
            return ScalarForElapsedTime(elapsed.TotalSeconds);
        }

        public float ScalarForElapsedTime(TimeSpan elapsed)
        {
            return ScalarForElapsedTime(elapsed.TotalSeconds);
        }

        public float ScalarForElapsedTime(double elapsed)
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
