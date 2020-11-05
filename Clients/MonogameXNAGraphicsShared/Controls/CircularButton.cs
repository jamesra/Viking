using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework;
using VikingXNA;

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


        public GridCircle Circle
        {
            get
            {
                return circleView.Circle;
            }
            set
            {
                circleView.Circle = value;
            }
        }

        /// <summary>
        /// Create a circle button with a default OnClick implementation that calls a simple action when clicked
        /// </summary>
        /// <param name="circle"></param>
        /// <param name="color"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static CircularButton CreateSimple(GridCircle circle, Microsoft.Xna.Framework.Color color, Action action)
        {
            CircularButton obj = new CircularButton(circle, color);
            obj.OnClick = new InputDeviceEventConsumerDelegate((sender, position, input_source, input_data) => { action(); return true; });
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
            CircularButton obj = new CircularButton(view);
            obj.OnClick = new InputDeviceEventConsumerDelegate((sender, position, input_source, input_data) => { action(); return true; });
            return obj;
        }

        public CircularButton(CircleView view, InputDeviceEventConsumerDelegate OnClick =null)
        {
            this.circleView = view;

            if(OnClick != null)
                this.OnClick = OnClick;
        }

        public CircularButton(GridCircle circle, Microsoft.Xna.Framework.Color color, InputDeviceEventConsumerDelegate OnClick =null)
        {
            this.circleView = new CircleView(circle, color);
            if(OnClick != null)
                this.OnClick += OnClick;
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return circleView.Circle.BoundingBox;
            }
        }

        public Color Color { get { return circleView.Color; } set { circleView.Color = value; } }
        public float Alpha { get { return circleView.Alpha; } set { circleView.Alpha = value; } }

        public bool Contains(GridVector2 Position)
        {
            return Circle.Intersects(Position);
        }
        
    }
}
