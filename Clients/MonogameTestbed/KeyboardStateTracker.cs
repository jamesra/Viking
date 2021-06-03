using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace MonogameTestbed
{
    class KeyboardStateTracker
    {
        private KeyboardState LastState;
        public KeyboardState CurrentState;

        private Dictionary<Keys, DateTime> KeyPressStart = new Dictionary<Keys, DateTime>();

        public void Update(KeyboardState state)
        {
            LastState = CurrentState;
            CurrentState = state;
            UpdatePressedKeys();
        }

        private void UpdatePressedKeys()
        {
            SortedSet<Keys> removedKeys = new SortedSet<Keys>(LastState.GetPressedKeys());
            removedKeys.ExceptWith(CurrentState.GetPressedKeys());

            //Remove the missing keys from the KeyPresStart times
            foreach(var key in removedKeys)
            {
                if(KeyPressStart.ContainsKey(key))
                {
                    KeyPressStart.Remove(key);
                }
            }

            foreach(var key in CurrentState.GetPressedKeys())
            {
                if(KeyPressStart.ContainsKey(key) == false)
                {
                    KeyPressStart[key] = DateTime.UtcNow;
                }
            }
        }

        public TimeSpan PressDuration(Keys key)
        {
            if(KeyPressStart.ContainsKey(key) == false)
            {
                return TimeSpan.Zero;
            }

            return DateTime.UtcNow - KeyPressStart[key];
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

        /// <summary>
        /// Returns true if the key's state transitioned from up to down since the last update.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Down(Keys key)
        {
            if (CurrentState == null)
                return false;

            return CurrentState.IsKeyDown(key);
        }
    }
}
