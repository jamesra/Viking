using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    public class GridPolyline : IPolyLine2D
    {
        List<IPoint2D> Points;

        public GridPolyline()
        {
            Points = new List<Geometry.IPoint2D>();
        }

        public GridPolyline(int capacity)
        {
            Points = new List<Geometry.IPoint2D>(capacity);
        }

        public GridPolyline(IEnumerable<IPoint2D> points)
        {
            Points = new List<Geometry.IPoint2D>(points);
        }

        public GridPolyline(IEnumerable<GridVector2> points)
        {
            Points = new List<Geometry.IPoint2D>(points.Cast<IPoint2D>());
        }

        public void Add(IPoint2D line)
        {
            Points.Add(line);
        }

        public double Area
        {
            get
            {
                throw new ArgumentException("No area for Polyline");
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                double MinX = Points.Min(p => p.X);
                double MaxX = Points.Max(p => p.X);
                double MinY = Points.Min(p => p.Y);
                double MaxY = Points.Max(p => p.Y);

                return new GridRectangle(MinX, MaxX, MinY, MaxY);
            }
        }

        public ShapeType2D ShapeType
        {
            get
            {
                return ShapeType2D.POLYLINE;
            }
        }

        public ICollection<ILineSegment2D> LineSegments
        {
            get
            {
                List<ILineSegment2D> listSegments = new List<ILineSegment2D>(this.Points.Count - 1);

                for (int i = 0; i < Points.Count - 1; i++)
                {
                    listSegments.Add(new GridLineSegment(Points[i], Points[i + 1] ));
                }

                return listSegments;
            }
        }

        ICollection<IPoint2D> IPolyLine2D.Points
        {
            get
            {
                return this.Points;
            }
        }

        public bool Contains(IPoint2D p)
        {
            return this.LineSegments.Any(line => line.Contains(p));
        }

        public bool Intersects(IShape2D shape)
        {
            return this.LineSegments.Any(line => line.Intersects(shape));
        }

        public IShape2D Translate(IPoint2D offset)
        {
            List<IPoint2D> translatedPoints = new List<Geometry.IPoint2D>(this.Points.Count);

            translatedPoints = this.Points.Select(p => new GridVector2(p.X + offset.X, p.Y + offset.Y)).Cast<IPoint2D>().ToList();

            return new GridPolyline(translatedPoints);
        }
    }
}
