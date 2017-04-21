using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public double Area
        {
            get
            {
                return Shapes.Sum(s => s.Area);
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return Shapes.Select(s => s.BoundingBox).Aggregate((bb1, bb2) => GridRectangle.Union(bb1,bb2));
            }
        }

        public ICollection<IShape2D> Geometries
        {
            get
            {
                return Shapes;
            }
        }

        public ShapeType2D ShapeType
        {
            get
            {
                return ShapeType2D.COLLECTION;
            }
        }

        public bool Contains(IPoint2D p)
        {
            return Shapes.Any(s => s.Contains(p));
        }

        public bool Intersects(IShape2D shape)
        {
            return Shapes.Any(s => s.Intersects(shape));
        }

        public IShape2D Translate(IPoint2D offset)
        {
            Shape2DCollection translatedShapes = new Shape2DCollection(Shapes.Count);
            foreach(IShape2D shape in Shapes)
            {
                translatedShapes.Add(shape.Translate(offset));
            }

            return translatedShapes;
        }
    }
}
