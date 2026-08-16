using System;
using System.Collections;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Enumerate all verticies for a collection of _shapes
    /// </summary>
    public class PolylineSetVertexEnum(IReadOnlyList<Polyline> shapes, int iStartingLineIndex = 0) : IEnumerator<PolylineIndex>, IEnumerator, IEnumerable<PolylineIndex>, IEnumerable
    {
        PolylineIndex? curIndex = null;

        readonly IReadOnlyList<Polyline> _shapes = shapes;

        /// <summary>
        /// The index to use for the first polygon in the list, defaults to zero
        /// </summary>
        private readonly int StartingLineIndex = iStartingLineIndex;

        public PolylineIndex Current
        {
            get
            {
                if (curIndex is null)
                {
                    throw new IndexOutOfRangeException("Current Index is undefined");
                }

                return curIndex.Value;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                if (curIndex is null)
                {
                    throw new IndexOutOfRangeException("Current Index is undefined");
                }

                return curIndex.Value;
            }
        }

        public void Dispose() => GC.SuppressFinalize(this);

        /// <summary>
        /// Go to the next index, if the shape is closed we do not return the closed index twice. 
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            if (curIndex is null)
            {
                if (_shapes.Count == 0)
                    return false;

                var line = _shapes[0];
                if (line.NumUniqueVertices == 0)
                    return false;

                curIndex = new PolylineIndex(StartingLineIndex, 0, line.NumUniqueVertices);
                return true;
            }

            PolylineIndex? next = NextIndex(_shapes, curIndex.Value);

            curIndex = next;
            return curIndex.HasValue;
        }

        private PolylineIndex? NextIndex(IReadOnlyList<Polyline> inputShapes, PolylineIndex current)
        {
            int iLine = current.ShapeIndex - StartingLineIndex;
            Polyline line = inputShapes[iLine];

            int iNextVert = current.VertexIndex + 1;


            if (iNextVert < line.NumUniqueVertices - 1)
            {
                //Move along the ring we are iterating
                return new PolylineIndex(current.ShapeIndex, iNextVert, line.NumUniqueVertices);
            }

            //Go to the next line
            int iNextLine = current.ShapeIndex + 1;
            while (iNextLine < inputShapes.Count)
            {
                if (inputShapes[iNextLine] is not null) //Skip over non-polylines
                    return new Geometry.PolylineIndex(iNextLine, 0, inputShapes[iNextLine].NumUniqueVertices);

                iNextLine++;
            }

            return new PolylineIndex?(); //We are out of indicies
        }

        public void Reset() => curIndex = new PolylineIndex?();

        public IEnumerator GetEnumerator() => (IEnumerator<PolylineIndex>)this;

        IEnumerator<PolylineIndex> IEnumerable<PolylineIndex>.GetEnumerator() => (IEnumerator<PolylineIndex>)this;
    }
}