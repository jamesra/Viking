using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;

namespace MonogameTestbed
{
    class GamePadStateTracker
    {
        private GamePadState LastState;
        public GamePadState CurrentState;
         
        static readonly PlayerIndex[] inputs = new PlayerIndex[] { PlayerIndex.One, PlayerIndex.Two, PlayerIndex.Three, PlayerIndex.Four };

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
            var padcaps = GamePad.GetCapabilities(PlayerIndex.One);
            if (GamePad.GetCapabilities(PlayerIndex.One).IsConnected)
            {
                return padcaps.HasLeftXThumbStick && padcaps.HasLeftYThumbStick && padcaps.HasRightXThumbStick && padcaps.HasRightYThumbStick;
            }

            return false;
        }


        public void Update(GamePadState state)
        {
            LastState = CurrentState;
            CurrentState = state; 
        }

        public bool Start_Clicked
        {
            get
            {
                return CurrentState.Buttons.Start == ButtonState.Pressed && (LastState.Buttons.Start != CurrentState.Buttons.Start);
            }
        }

        public bool Back_Clicked
        {
            get
            {
                return CurrentState.Buttons.Back == ButtonState.Pressed && (LastState.Buttons.Back != CurrentState.Buttons.Back);
            }
        }

        public bool A_Clicked
        {
            get
            {
                return CurrentState.Buttons.A == ButtonState.Pressed && (LastState.Buttons.A != CurrentState.Buttons.A);
            }
        }

        public bool B_Clicked
        {
            get
            {
                return CurrentState.Buttons.B == ButtonState.Pressed && (LastState.Buttons.B != CurrentState.Buttons.B);
            }
        }

        public bool X_Clicked
        {
            get
            {
                return CurrentState.Buttons.X == ButtonState.Pressed && (LastState.Buttons.X != CurrentState.Buttons.X);
            }
        }

        public bool Y_Clicked
        {
            get
            {
                return CurrentState.Buttons.Y == ButtonState.Pressed && (LastState.Buttons.Y != CurrentState.Buttons.Y);
            }
        }

        public bool LeftShoulder_Clicked
        {
            get
            {
                return CurrentState.Buttons.LeftShoulder == ButtonState.Pressed && 
                    (LastState.Buttons.LeftShoulder != CurrentState.Buttons.LeftShoulder);
            }
        }

        public bool RightShoulder_Clicked
        {
            get
            {
                return CurrentState.Buttons.RightShoulder == ButtonState.Pressed &&
                    (LastState.Buttons.RightShoulder != CurrentState.Buttons.RightShoulder);
            }
        }

        public bool LeftStick_Clicked
        {
            get
            {
                return CurrentState.Buttons.LeftStick == ButtonState.Pressed &&
                    (LastState.Buttons.LeftStick != CurrentState.Buttons.LeftStick);
            }
        }
        
        public bool RightStick_Clicked
        {
            get
            {
                return CurrentState.Buttons.RightStick == ButtonState.Pressed &&
                    (LastState.Buttons.RightStick != CurrentState.Buttons.RightStick);
            }
        } 

        public bool LeftTriggerPulled
        {
            get
            {
                return CurrentState.Triggers.Left > 0;
            }
        }

        public bool RightTriggerPulled
        {
            get
            {
                return CurrentState.Triggers.Right > 0;
            }
        }
    }
}
