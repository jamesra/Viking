using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    public class Shape2DCollection : IShapeCollection2D
    {
        public List<IShape2D> Shapes;

        public Shape2DCollection()
        {
            Shapes = new List<Geometry.IShape2D>();
        }

        public Shape2DCollection(int capacity)
        {
            Shapes = new List<Geometry.IShape2D>(capacity);
        }

        public Shape2DCollection(ICollection<IShape2D> shapes)
        {
            Shapes = new List<Geometry.IShape2D>(shapes.Count);
            Shapes.AddRange(shapes);
        }

        public void Add(IShape2D shape)
        {
            Shapes.Add(shape);
        }

        public void AddRange(IEnumerable<IShape2D> shapes)
        {
            Shapes.AddRange(shapes);
        }

        public void Remove(IShape2D shape)
        {
            Shapes.Remove(shape);
        }

        public double Area => Shapes.Sum(s => s.Area);

        public GridRectangle BoundingBox => Shapes.Select(s => s.BoundingBox).Aggregate((bb1, bb2) => GridRectangle.Union(bb1, bb2));

        public IList<IShape2D> Geometries => Shapes;

        public virtual ShapeType2D ShapeType => ShapeType2D.COLLECTION;

        GridVector2 IShape2D.Centroid => GridVector2.Average(Shapes.Select(s => s.Centroid));

        public bool Contains(in IPoint2D p)
        {
            IPoint2D pnt = p;
            return Shapes.Any(s => s.Contains(pnt));
        }

        public bool Intersects(in IShape2D shape)
        {
            IShape2D shp = shape;
            return Shapes.Any(s => s.Intersects(shp));
        }

        public IShape2D Translate(in IPoint2D offset)
        {
            Shape2DCollection translatedShapes = new Shape2DCollection(Shapes.Count);
            foreach (IShape2D shape in Shapes)
            {
                translatedShapes.Add(shape.Translate(offset));
            }

            return translatedShapes;
        }

        public bool Equals(IShape2D other)
        {
            if (other is IShapeCollection2D otherColl)
                return Equals(other);

            return false;
        }

        public bool Equals(IShapeCollection2D other)
        {
            if (other is null)
                return false;

            if (this.Shapes.Count != other.Geometries.Count)
                return false;

            for (int i = 0; i < Shapes.Count; i++)
            {
                bool equal = Shapes[i].Equals((other.Geometries[i]));
                if (!equal) return false;
            }

            return true; 
        }
    }
}
