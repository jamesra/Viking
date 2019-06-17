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
        LEFT,
        MIDDLE,
        RIGHT
    };

    public class CircularButton : ICanvasView
    {
        public CircleView circleView = null;

        public delegate void OnClickEventHandler(object sender, MouseButton button);
        public event OnClickEventHandler OnClick; 

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

        public CircularButton(TextureCircleView view)
        {
            this.circleView = view;
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
        
        public void TestClick(GridVector2 point, MouseButton button)
        {
            if(this.Intersects(point))
            {
                OnClick(this, button);
            }
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

        public bool Intersects(GridVector2 Position)
        {
            return Circle.Intersects(Position);
        }

        public bool IsVisible(Scene scene)
        {
            return circleView.IsVisible(scene);
        }
    }
}
