using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonogameTestbed
{
    class GamePadStateTracker
    {
        private GamePadState LastState;
        public GamePadState CurrentState;
         
        static readonly PlayerIndex[] inputs = new PlayerIndex[] { PlayerIndex.One, PlayerIndex.Two, PlayerIndex.Three, PlayerIndex.Four };

        private static bool ControllerErrorPrinted = false;

        public static PlayerIndex? GetFirstConnectedController()
        {
            foreach (var input in inputs)
            {
                if (IsValidController(input))
                    return input;
            }

            return new PlayerIndex?();
        }

        private static bool IsValidController(PlayerIndex index)
        {
            if (ControllerErrorPrinted)
                return false;

            try
            {
                var padcaps = GamePad.GetCapabilities(PlayerIndex.One);
                if (GamePad.GetCapabilities(PlayerIndex.One).IsConnected)
                {
                    return padcaps.HasLeftXThumbStick && padcaps.HasLeftYThumbStick && padcaps.HasRightXThumbStick &&
                           padcaps.HasRightYThumbStick;
                }
            }
            catch (Exception e)
            {
                if(false == ControllerErrorPrinted)
                    Console.WriteLine($"Unable to initialize controller input.  The program will still run but a gamepad cannot be used for input.\n{e}");

                ControllerErrorPrinted = true;
                return false;
            }

            return false;
        }


        public void Update(GamePadState state)
        {
            LastState = CurrentState;
            CurrentState = state;  
        }

        public bool Start_Clicked => CurrentState.Buttons.Start == ButtonState.Pressed && (LastState.Buttons.Start != CurrentState.Buttons.Start);

        public bool Back_Clicked => CurrentState.Buttons.Back == ButtonState.Pressed && (LastState.Buttons.Back != CurrentState.Buttons.Back);

        public bool A_Clicked => CurrentState.Buttons.A == ButtonState.Pressed && (LastState.Buttons.A != CurrentState.Buttons.A);

        public bool B_Clicked => CurrentState.Buttons.B == ButtonState.Pressed && (LastState.Buttons.B != CurrentState.Buttons.B);

        public bool X_Clicked => CurrentState.Buttons.X == ButtonState.Pressed && (LastState.Buttons.X != CurrentState.Buttons.X);

        public bool Y_Clicked => CurrentState.Buttons.Y == ButtonState.Pressed && (LastState.Buttons.Y != CurrentState.Buttons.Y);

        public bool LeftShoulder_Clicked =>
            CurrentState.Buttons.LeftShoulder == ButtonState.Pressed && 
            (LastState.Buttons.LeftShoulder != CurrentState.Buttons.LeftShoulder);

        public bool RightShoulder_Clicked =>
            CurrentState.Buttons.RightShoulder == ButtonState.Pressed &&
            (LastState.Buttons.RightShoulder != CurrentState.Buttons.RightShoulder);

        public bool LeftStick_Clicked =>
            CurrentState.Buttons.LeftStick == ButtonState.Pressed &&
            (LastState.Buttons.LeftStick != CurrentState.Buttons.LeftStick);

        public bool RightStick_Clicked =>
            CurrentState.Buttons.RightStick == ButtonState.Pressed &&
            (LastState.Buttons.RightStick != CurrentState.Buttons.RightStick);

        public bool LeftTriggerPulled => CurrentState.Triggers.Left > 0;

        public bool RightTriggerPulled => CurrentState.Triggers.Right > 0;
    }
}
