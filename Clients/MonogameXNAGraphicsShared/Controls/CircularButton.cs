using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
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
    /// A circular button control that can be clicked by the user and fires an event
    /// </summary>
    public class CircularButton : ICanvasView, IClickable
    {
        public CircleView circleView = null;
         
        public event Action<IClickable, object> OnClick; 

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
        
        public CircularButton(CircleView view, Action<IClickable, object> OnClick =null)
        {
            this.circleView = view;

            if(OnClick != null)
                this.OnClick += OnClick;
        }

        public CircularButton(GridCircle circle, Microsoft.Xna.Framework.Color color, Action<IClickable, object> OnClick =null)
        {
            this.circleView = new CircleView(circle, color);
            if(OnClick != null)
                this.OnClick += OnClick;
        } 

        public int VisualHeight
        {
            get; set;
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return circleView.Circle.BoundingBox;
            }
        }
        
        /// <summary>
        /// Called by the owner window or control.  Will fire the Button's OnClick Event if 
        /// the point is within the button
        /// </summary>
        /// <param name="point"></param>
        /// <param name="button"></param>
        /// <returns>True if the button contained the point and an event was fired</returns>
        public bool TryMouseClick(GridVector2 point, MouseButton buttons)
        {
            if(this.Contains(point))
            {
                if(OnClick != null)
                    OnClick(this, buttons);  

                return true;
            }

            return false;
        }

        /// <summary>
        /// Called by the owner window or control.  Will fire the Button's OnClick Event if 
        /// the point is within the button
        /// </summary>
        /// <param name="point"></param>
        /// <param name="button"></param>
        /// <returns>True if the button contained the point and an event was fired</returns>
        public bool TryPenClick(GridVector2 point, object pen_info=null)
        {
            //TODO: Move PenEventArgs to an assembly visible by VikingXNAGraphics
            if (this.Contains(point))
            {
                if (OnClick != null)
                    OnClick(this, null);

                return true;
            }

            return false;
        }

        public double Distance(GridVector2 Position)
        {
            return circleView.Circle.Distance(Position);
        }

        public double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return GridVector2.Distance(Position, this.Circle.Center) / this.Circle.Radius;
        }

        public double DistanceToCenter(GridVector2 Position)
        {
            return GridVector2.Distance(Position, this.Circle.Center);
        }

        public bool Contains(GridVector2 Position)
        {
            return Circle.Intersects(Position);
        }

        public bool Intersects(GridLineSegment line)
        {
            return Circle.Intersects(line);
        }

        public bool IsVisible(Scene scene)
        {
            return circleView.IsVisible(scene);
        }
    }
}
