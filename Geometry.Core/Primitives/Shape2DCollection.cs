using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Heterogeneous set of 2D shapes. Point/line <see cref="GetRelation"/> ORs child flags.
    /// </summary>
    public class Shape2DCollection : IShapeCollection2D
    {
        readonly List<IShape2D> _shapes;

        public Shape2DCollection()
        {
            _shapes = [];
        }

        public Shape2DCollection(int capacity)
        {
            _shapes = new List<Geometry.IShape2D>(capacity);
        }

        public Shape2DCollection(ICollection<IShape2D> shapes)
        {
            _shapes = [.. shapes];
        }

        public void Add(IShape2D shape) => _shapes.Add(shape);

        public void AddRange(IEnumerable<IShape2D> shapes) => _shapes.AddRange(shapes);

        public void Remove(IShape2D shape) => _shapes.Remove(shape);

        public double Area => _shapes.Sum(s => s.Area);

        public Rectangle BoundingBox => _shapes.Select(s => s.BoundingBox).Aggregate((bb1, bb2) => Rectangle.Union(bb1, bb2));

        public IList<IShape2D> Geometries => _shapes;

        public virtual ShapeType2D ShapeType => ShapeType2D.Collection;

        public bool Contains(in IPoint2D p)
        {
            IPoint2D pnt = p;
            return _shapes.Any(s => s.Contains(pnt));
        }

        public bool Covers(in IPoint2D p)
        {
            IPoint2D pnt = p;
            return _shapes.Any(s => s.Covers(pnt));
        }

        /// <summary>
        /// ORs child relations so a collection can report interior, boundary, and crossing together.
        /// Walks every child.
        /// </summary>
        public ShapeRelation GetRelation(in IPoint2D p)
        {
            Trace.WriteLine("GetRelation on a Shape2DCollection is computationally expensive");
            ShapeRelation output = ShapeRelation.None;
            foreach (var s in _shapes)
            {
                var result = s.GetRelation(p);
                output |= result;
            }

            return output;
        }

        /// <summary>
        /// ORs child relations for a line. Walks every child.
        /// </summary>
        public ShapeRelation GetRelation(in ILineSegment2D line)
        {
            Trace.WriteLine("GetRelation on a Shape2DCollection is computationally expensive");
            ShapeRelation output = ShapeRelation.None;
            foreach (var s in _shapes)
            {
                var result = s.GetRelation(line);
                output |= result;
            }

            return output;
        }

        public bool Intersects(in IShape2D shape)
        {
            IShape2D shp = shape;
            return _shapes.Any(s => s.Intersects(shp));
        }

        public IShape2D Translate(in IPoint2D offset)
        {
            Shape2DCollection translated_shapes = new(_shapes.Count);
            foreach (IShape2D shape in _shapes)
            {
                translated_shapes.Add(shape.Translate(offset));
            }

            return translated_shapes;
        }

        public bool Equals(IShape2D other)
        {
            if (other is IShapeCollection2D otherColl)
                return Equals(other);

            return false;
        }

        public bool Equals(IShapeCollection2D other)
        {
            if (this._shapes.Count != other.Geometries.Count)
                return false;

            for (int i = 0; i < _shapes.Count; i++)
            {
                bool equal = _shapes[i].Equals((other.Geometries[i]));
                if (!equal) return false;
            }

            return true;
        }
    }
}
