using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using System;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics.Controls
{
    public enum MouseButton
    {
        NONE,
        LEFT,
        MIDDLE,
        RIGHT,
        X1,
        X2
    };




    /// <summary>
    /// Pairs the view of a circular button control with support for clicking the button
    /// </summary>
    public class CircularButton : IClickable, IColorView
    {
        public CircleView circleView = null;

        public InputDeviceEventConsumerDelegate OnClick { get; set; } = null;


        public Circle Circle
        {
            get => circleView.Circle;
            set => circleView.Circle = value;
        }

        /// <summary>
        /// Create a circle button with a default OnClick implementation that calls a simple action when clicked
        /// </summary>
        /// <param name="circle"></param>
        /// <param name="color"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static CircularButton CreateSimple(Circle circle, Microsoft.Xna.Framework.Color color, Action action)
        {
            CircularButton obj = new(circle, color)
            {
                OnClick = new InputDeviceEventConsumerDelegate((sender, position, input_source, input_data) => { action(); return true; })
            };
            return obj;
        }

        /// <summary>
        /// Create a circle button with a default OnClick implementation that calls a simple action when clicked
        /// </summary>
        /// <param name="circle"></param>
        /// <param name="color"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static CircularButton CreateSimple(CircleView view, Action action)
        {
            CircularButton obj = new(view)
            {
                OnClick = new InputDeviceEventConsumerDelegate((sender, position, input_source, input_data) => { action(); return true; })
            };
            return obj;
        }

        public CircularButton(CircleView view, InputDeviceEventConsumerDelegate OnClick = null)
        {
            this.circleView = view;

            if (OnClick != null)
                this.OnClick = OnClick;
        }

        public CircularButton(Circle circle, Microsoft.Xna.Framework.Color color, InputDeviceEventConsumerDelegate OnClick = null)
        {
            this.circleView = new CircleView(circle, color);
            if (OnClick != null)
                this.OnClick += OnClick;
        }

        public Rectangle BoundingBox => circleView.Circle.BoundingBox;

        public Color Color
        {
            get => circleView.Color;
            set => circleView.Color = value;
        }
        public float Alpha
        {
            get => circleView.Alpha;
            set => circleView.Alpha = value;
        }

        public bool Contains(Geometry.Vector2 Position) => Circle.Intersects(Position);

    }
}
