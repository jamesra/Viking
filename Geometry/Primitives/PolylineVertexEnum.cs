using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Enumerates verticies of a polygon, starting with verticies on the exterior ring and then continuing to verticies on any interior rings
    /// </summary>
    public class PolylineVertexEnum : IEnumerator<PolylineIndex>, IEnumerator, IEnumerable<PolylineIndex>, IEnumerable
    {
        PolylineIndex? curIndex;
        readonly GridPolyline polyline;

        /// <summary>
        /// If set indicies returned by this enumerator will use this value for the iPoly field of the polygon index
        /// </summary>
        public int? shapeIndex = new int?();

        public bool Reverse = false;

        public PolylineVertexEnum(GridPolyline line, bool reverse = false)
        {
            this.polyline = line;
            curIndex = new Geometry.PolylineIndex?();
            Reverse = reverse;
        }

        public PolylineVertexEnum(GridPolyline line, int ForceiPoly, bool reverse = false)
        {
            this.polyline = line;
            curIndex = new Geometry.PolylineIndex?();
            shapeIndex = ForceiPoly;
            Reverse = reverse;
        }

        public PolylineIndex Current
        {
            get
            {
                if (!curIndex.HasValue)
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
                if (!curIndex.HasValue)
                {
                    throw new IndexOutOfRangeException("Current Index is undefined");
                }

                return curIndex.Value;
            }
        }

        public void Dispose() { }

        /// <summary>
        /// Go to the next index, if the shape is closed we do not return the closed index twice. 
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            if (!curIndex.HasValue)
            {
                if (polyline == null)
                    return false;

                if (polyline.PointCount == 0)
                    return false;

                if (false == Reverse)
                {
                    curIndex = new PolylineIndex(shapeIndex.HasValue ? shapeIndex.Value : 0, 0, polyline.PointCount - 1);
                    return true;
                }
                else
                { 
                    curIndex = new PolylineIndex(shapeIndex.HasValue ? shapeIndex.Value : 0, polyline.PointCount - 1, polyline.PointCount - 1); 
                }
            }

            curIndex = Reverse ? PrevIndex(polyline, curIndex.Value) : NextIndex(polyline, curIndex.Value);

            return curIndex.HasValue;
        }

        private PolylineIndex? PrevIndex(GridPolyline poly, PolylineIndex current)
        {
            int iPrevIndex = current.iVertex - 1;

            if (iPrevIndex >= 0) //We still have verticies on our current ring, so move back one step
            {
                //Move along the ring we are iterating
                return new PolylineIndex(current.iLine, iPrevIndex, current.NumUnique);
            }
            else
            { 
                    //We finished enumerating the exterior ring.  Normally this is where we g to the previous shape but since this enumerator only covers a single shape we are done.
                    return new PolylineIndex?(); 
            }
        }

        private PolylineIndex? NextIndex(GridPolyline poly, PolylineIndex current)
        {
            int iNextVert = current.iVertex + 1;

            if (iNextVert < current.NumUnique) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Move along the ring we are iterating
                return new PolylineIndex(current.iLine, iNextVert, current.NumUnique);
            }

            if (iNextVert == current.NumUnique) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Check for the case where the final vertex in the ring is not equal to the first.
                if (poly.Points[0] != poly.Points[iNextVert])
                    return new PolylineIndex(current.iLine, iNextVert, current.NumUnique);
            }
              
            //Normally this is where we go to the next shape but since this enumerator only covers a single shape we are done.
            return new PolylineIndex?();
        }

        public void Reset()
        {
            curIndex = new PolylineIndex?();
        }

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator<PolylineIndex>)this;
        }

        IEnumerator<PolylineIndex> IEnumerable<PolylineIndex>.GetEnumerator()
        {
            return (IEnumerator<PolylineIndex>)this;
        }
    }
}
