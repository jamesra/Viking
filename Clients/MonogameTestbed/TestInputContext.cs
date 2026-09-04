using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using VikingXNA;

namespace MonogameTestbed
{
    /// <summary>
    /// Shared gamepad / keyboard / 2D-camera input for IGraphicsTest Update loops.
    /// </summary>
    class TestInputContext
    {
        public readonly GamePadStateTracker Gamepad = new();
        public readonly KeyboardStateTracker Keyboard = new();
        public readonly Cursor2DCameraManipulator CameraManipulator = new();

        public PlayerIndex ControllerIndex =>
            GamePadStateTracker.GetFirstConnectedController() ?? PlayerIndex.One;

        public GamePadState PollGamePad() => GamePad.GetState(ControllerIndex);

        public GamePadState UpdateTrackers()
        {
            GamePadState state = PollGamePad();
            Gamepad.Update(state);
            Keyboard.Update(Microsoft.Xna.Framework.Input.Keyboard.GetState());
            return state;
        }

        public GamePadState Update(Scene scene)
        {
            GamePadState state = UpdateTrackers();
            CameraManipulator.Update(scene.Camera);
            return state;
        }
    }
}
