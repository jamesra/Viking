using System;
using System.Collections;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Enumerate all verticies for a collection of _shapes
    /// </summary>
    public class ShapeSetVertexEnum(in IReadOnlyList<IShape2D> shapes, int iStartingShapeIndex = 0) : IEnumerator<IShapeIndex>, IEnumerator, IEnumerable<IShapeIndex>, IEnumerable
    {
        IShapeIndex _curIndex = null;

        readonly IReadOnlyList<IShape2D> _shapes = shapes;

        /// <summary>
        /// The index we are at in the list. Defaults to zero
        /// </summary>
        private int _currentShapeIndex = iStartingShapeIndex;

        private IEnumerator _enumerator = null;

        public IShapeIndex Current
        {
            get
            {
                if (_curIndex is null)
                {
                    throw new IndexOutOfRangeException("Current Index is undefined");
                }

                return _curIndex;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                if (_curIndex is null)
                    throw new IndexOutOfRangeException("Current Index is undefined");

                return _curIndex;
            }
        }

        public void Dispose() => GC.SuppressFinalize(this);

        /// <summary>
        /// Go to the next index, if the shape is closed we do not return the closed index twice. 
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            if (_curIndex is null)
            {
                if (_shapes.Count <= _currentShapeIndex)
                    return false;

                _enumerator = GetNextEnumerator(_currentShapeIndex);
            }

            //We have an existing _enumerator
            if (_enumerator.MoveNext())
            {
                _curIndex = (IShapeIndex)_enumerator.Current;
                return true;
            }
            else
            {
                _currentShapeIndex += 1;
                if (_currentShapeIndex >= _shapes.Count)
                    return false;

                _enumerator = GetNextEnumerator(_currentShapeIndex);
                return MoveNext();
            }
        }

        private IEnumerator GetNextEnumerator(int iShape)
        {
            IShape2D shape = _shapes[_currentShapeIndex];
            if (shape.ShapeType == ShapeType2D.Polygon)
            {
                if (shape is Polygon poly)
                {
                    return new PolygonVertexEnum(poly, iShape, false).GetEnumerator();
                }

                throw new ArgumentException("Expected Polygon");
            }
            else if (shape.ShapeType == ShapeType2D.Polyline)
            {
                if (shape is Polyline line)
                {
                    return new PolylineVertexEnum(line, iShape, false).GetEnumerator();
                }

                throw new ArgumentException("Expected Polyline");
            }
            else
            {
                return Array.Empty<IShapeIndex>().GetEnumerator();
            }
        }


        public void Reset() => _curIndex = null;

        public IEnumerator GetEnumerator() => (IEnumerator<PolygonIndex>)this;

        IEnumerator<IShapeIndex> IEnumerable<IShapeIndex>.GetEnumerator() => (IEnumerator<IShapeIndex>)this;
    }
}