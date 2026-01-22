using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace MonogameTestbed
{
    /// <summary>
    /// Lists the buttons we know about on a mouse
    /// </summary>
    [Flags]
    public enum MouseButton
    {
        None = 0,
        Left = 1,
        Middle = 2,
        Right = 3,
        X1 = 4,
        X2 = 5
    }

    /// <summary>
    /// Lists the states a mouse button can be in
    /// These are flags which are converted to numbers by combining the "ButtonState.Pressed" bit of the current and previous mouse state:
    /// CurrentState.HasFlag(ButtonStatePressed) < 1 | LastState.HasFlag(ButtonStatePressed)
    /// </summary>
    [Flags]
    public enum MouseButtonStatus
    {
        /// <summary>
        /// Undefined state
        /// </summary>
        None = -1,
        /// <summary>
        /// Mouse button was up and remained up since last update
        /// 0 =  < 1 | LastState
        /// </summary>
        Up = 0,
        /// <summary>
        /// Mouse was pressed since last update
        /// </summary>
        Clicked = 1,
        /// <summary>
        /// Mouse button was released since last update
        /// </summary>
        Released = 2,
        /// <summary>
        /// Mouse button was down and remained down since last update
        /// </summary>
        Down = 3
    }

    public static class MouseButtonStateExtensions
    {
        public static MouseButtonStatus ReadStatus(this MouseButton btn, MouseStateHelper CurrentState, MouseStateHelper LastState)
        {
            bool wasPressed = LastState[btn].HasFlag(ButtonState.Pressed);
            bool isPressed = CurrentState[btn].HasFlag(ButtonState.Pressed);

            MouseButtonStatus result = (MouseButtonStatus)(((isPressed ? 1 : 0) << 1) | (wasPressed ? 1 : 0));
            return result;
        }

    }

    public class MouseStateHelper(MouseState state)
    {
        readonly MouseState State = state;

        public int X => State.X;

        //
        // Summary:
        //     Gets vertical position of the cursor.
        public int Y => State.Y;

        //
        // Summary:
        //     Gets cursor position.
        public Point Position => State.Position;

        public int ScrollWheelValue => State.ScrollWheelValue;

        public ButtonState this[MouseButton index]
        {
            get
            {
                return index switch
                {
                    MouseButton.Left => State.LeftButton,
                    MouseButton.Middle => State.MiddleButton,
                    MouseButton.Right => State.RightButton,
                    MouseButton.X1 => State.XButton1,
                    MouseButton.X2 => State.XButton2,
                    _ => throw new NotImplementedException($"Unexpected button requested {index}"),
                };
            }
        }
    }

    class MouseButtonList<T> : List<T>
    {
        public MouseButtonList()
        {
        }

        public MouseButtonList(int capacity) : base(capacity)
        {
            for (int i = 0; i < capacity; i++)
            {
                this.Add(default);
            }
        }

        public MouseButtonList(IEnumerable<T> collection) : base(collection)
        {
        }

        public T this[MouseButton index]
        {
            get => this[(int)index];
            set => this[(int)index] = value;
        }

    }



    class MouseStateTracker
    {
        private MouseStateHelper LastState;
        MouseStateHelper CurrentState;

        private static readonly int NumButtons = typeof(MouseButton).GetEnumValues().Length;

        /// <summary>
        /// True if the button was pressed last update
        /// </summary>
        public MouseButtonList<bool> Clicked = new(NumButtons);

        /// <summary>
        /// True if the button was released last update
        /// </summary>
        public MouseButtonList<bool> Released = new(NumButtons);

        /// <summary>
        /// True if the button is down and its state did not change last update
        /// </summary>
        public MouseButtonList<bool> Down = new(NumButtons);

        /// <summary>
        /// True if the button is not pressed and its state did not change last update
        /// </summary>
        public MouseButtonList<bool> Up = new(NumButtons);

        /// <summary>
        /// True if the button is not pressed and its state did not change last update
        /// </summary>
        public MouseButtonList<MouseButtonStatus> ButtonStatus = new(NumButtons);

        /// <summary>
        /// The length of time the button has been in the current state
        /// </summary>
        public MouseButtonList<DateTime> ButtonStateStartTime = new(NumButtons);


        public Point Positon => CurrentState.Position;
        public int X => CurrentState.X;
        public int Y => CurrentState.Y;

        public int ScrollWheelValue => CurrentState.ScrollWheelValue;

        public int ScrollWheelValueDelta => LastState.ScrollWheelValue - CurrentState.ScrollWheelValue;

        public Point PositionDelta =>
            new(LastState.X - CurrentState.X,
                LastState.Y - CurrentState.Y);

        public void Update(MouseState state)
        {
            LastState = CurrentState;
            CurrentState = new MouseStateHelper(state);

            LastState ??= CurrentState;

            foreach (var idx in Enum.GetValues(typeof(MouseButton)))
            {
                MouseButton btn = (MouseButton)idx;
                int i = (int)btn;

                if (btn == MouseButton.None)
                    continue;

                bool wasPressed = LastState[btn].HasFlag(ButtonState.Pressed);
                bool isPressed = CurrentState[btn].HasFlag(ButtonState.Pressed);

                MouseButtonStatus btnStatus = btn.ReadStatus(CurrentState, LastState);
                if (btnStatus != ButtonStatus[btn])
                {
                    ButtonStateStartTime[btn] = DateTime.UtcNow;
                    ButtonStatus[btn] = btnStatus;
                }

                Clicked[btn] = btnStatus == MouseButtonStatus.Clicked;
                Released[btn] = btnStatus == MouseButtonStatus.Released;
                Down[btn] = btnStatus == MouseButtonStatus.Down;
                Up[btn] = btnStatus == MouseButtonStatus.Up;
            }
        }

        /// <summary>
        /// Returns the length of time a button has been down
        /// </summary>
        /// <param name="btn">The button we are inquiring about</param>
        /// <returns></returns>
        public TimeSpan ButtonStateDuration(MouseButton btn) => DateTime.UtcNow - ButtonStateStartTime[btn];

    }
}
