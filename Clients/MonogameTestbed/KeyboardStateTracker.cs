using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;

namespace MonogameTestbed
{
    class KeyboardStateTracker
    {
        private KeyboardState LastState;
        public KeyboardState CurrentState;

        public void Update(KeyboardState state)
        {
            LastState = CurrentState;
            CurrentState = state;
        }

        /// <summary>
        /// Returns true if the key's state transitioned from up to down since the last update.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Pressed(Keys key)
        {
            if (CurrentState == null)
                return false; 

            if (LastState == null)
                return CurrentState.IsKeyDown(key);

            if(LastState.IsKeyDown(key) == false && CurrentState.IsKeyDown(key) == true )
            {
                return true;
            }

            return false; 
        }
    }
}
