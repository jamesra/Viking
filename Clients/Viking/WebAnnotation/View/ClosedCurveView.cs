using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Geometry;
using WebAnnotation.ViewModel;
using Viking.Common;

namespace WebAnnotation.View
{
    /// <summary>
    /// Draws a closed curve through the control points using Catmull-rom
    /// </summary>
    class ClosedCurveView : System.Windows.IWeakEventListener
    {
        public ObservableCollection<GridVector2> ControlPoints;

        private List<GridVector2> CurvePoints = new List<GridVector2>();

        public double LineWidth;
        public Microsoft.Xna.Framework.Color Color;
        public int NumInterpolations = 5;

        public ClosedCurveView(ObservableCollection<GridVector2> controlPoints, Microsoft.Xna.Framework.Color color, double lineWidth=16.0, int numInterpolations=5)
        {
            ControlPoints = controlPoints;
            this.Color = color;
            this.LineWidth = lineWidth;
            this.NumInterpolations = numInterpolations;

            NotifyCollectionChangedEventManager.AddListener(this.ControlPoints, this);
        }
         
        private static List<GridVector2> CalculateCurvePoints(ICollection<GridVector2> ControlPoints, int NumInterpolations)
        {
            List<GridVector2> CurvePoints = new List<GridVector2>(ControlPoints.Count);
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new List<GridVector2>(ControlPoints);
            }
            if (ControlPoints.Count == 3)
            {
                CurvePoints = Geometry.Lagrange.FitCurve(ControlPoints.ToArray(), NumInterpolations * ControlPoints.Count).ToList();
            }
            else if (ControlPoints.Count > 3)
            {
                CurvePoints = Geometry.CatmullRom.FitCurve(ControlPoints.ToArray(), NumInterpolations, true).ToList();
                CurvePoints.Add(CurvePoints.First());
            }

            return CurvePoints; 
        }

        /// <summary>
        /// Fetch the control point color to use for a line
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private static Microsoft.Xna.Framework.Color ControlPointColor(Microsoft.Xna.Framework.Color color)
        {
            //return new Microsoft.Xna.Framework.Color(255 - (int)color.A, 255 - (int)color.G, 255 - (int)color.B, (int)color.A);
            return color;
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device, RoundLineCode.RoundLineManager LineManager, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect, ICollection<GridVector2> ControlPoints, int NumInterpolations, Microsoft.Xna.Framework.Color Color, double LineWidth = 16.0)
        {
            List<GridVector2> CurvePoints = CalculateCurvePoints(ControlPoints, NumInterpolations);

            
            Microsoft.Xna.Framework.Color pointColor = ControlPointColor(Color);
            //GlobalPrimitives.DrawPoints(LineManager, basicEffect, ControlPoints.ToList(), LineWidth, pointColor); 
            
            foreach (GridVector2 cp in ControlPoints)
            {
                GlobalPrimitives.DrawCircle(device, basicEffect, cp, LineWidth, pointColor);
            }
            

            GlobalPrimitives.DrawPolyline(LineManager, basicEffect, CurvePoints, LineWidth, Color);
        }

        public void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device, RoundLineCode.RoundLineManager LineManager, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            Microsoft.Xna.Framework.Color pointColor = ControlPointColor(Color);
            //GlobalPrimitives.DrawPoints(LineManager, basicEffect, ControlPoints, LineWidth, pointColor);
            
            foreach (GridVector2 cp in ControlPoints)
            {
                GlobalPrimitives.DrawCircle(device, basicEffect, cp, LineWidth, pointColor);
            }
            
            GlobalPrimitives.DrawPolyline(LineManager, basicEffect, CurvePoints, this.LineWidth, this.Color);
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            if (sender == null)
                throw new ArgumentNullException("sender");

            System.Collections.Specialized.NotifyCollectionChangedEventArgs CollectionChangeArgs = e as System.Collections.Specialized.NotifyCollectionChangedEventArgs;
            if (CollectionChangeArgs != null)
            {
                this.CurvePoints = CalculateCurvePoints(this.ControlPoints, this.NumInterpolations);
                return true;
            }

            return false;
        }
    }
}
