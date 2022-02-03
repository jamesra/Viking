using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Enumerate all verticies for a collection of polygons
    /// </summary>
    public class PolySetVertexEnum : IEnumerator<PolygonIndex>, IEnumerator, IEnumerable<PolygonIndex>, IEnumerable
    {
        PolygonIndex? curIndex;

        readonly IReadOnlyList<GridPolygon> polygons;

        /// <summary>
        /// The index to use for the first polygon in the list, defaults to zero
        /// </summary>
        private readonly int StartingPolyIndex;


        public PolySetVertexEnum(IReadOnlyList<GridPolygon> polys, int iStartingPolyIndex = 0)
        {
            this.polygons = polys;
            curIndex = new Geometry.PolygonIndex?();
            StartingPolyIndex = iStartingPolyIndex;
        }

        public PolygonIndex Current
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

        public void Dispose() { GC.SuppressFinalize(this); }

        /// <summary>
        /// Go to the next index, if the shape is closed we do not return the closed index twice. 
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            if (!curIndex.HasValue)
            {
                if (polygons.Count == 0)
                    return false;

                if (polygons[0].ExteriorRing.Length == 0)
                    return false;

                curIndex = new PolygonIndex(StartingPolyIndex, 0, polygons[0].ExteriorRing.Length - 1);
                return true;
            }

            PolygonIndex? next = NextIndex(polygons, curIndex.Value);

            curIndex = next;
            return curIndex.HasValue;
        }

        private PolygonIndex? NextIndex(IReadOnlyList<GridPolygon> inputPolygons, PolygonIndex current)
        {
            int iPoly = current.iPoly - StartingPolyIndex;
            GridPolygon poly = inputPolygons[iPoly];

            int iNextVert = current.iVertex + 1;
            GridVector2[] ring = current.GetRing(poly);

            if (iNextVert < ring.Length - 1) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Move along the ring we are iterating
                return new PolygonIndex(current.iPoly, current.iInnerPoly, iNextVert, ring.Length - 1);
            }

            if (iNextVert == ring.Length - 1) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Move along the ring we are iterating and hit the last vertex in a closed loop that equals the first vertex
                if (ring[0] != ring[iNextVert])
                    return new PolygonIndex(current.iPoly, current.iInnerPoly, iNextVert, ring.Length - 1);
            }

            //OK, handle case where we are out of indicies on the current ring            
            {
                //Find the next ring
                if (current.IsInner)
                {
                    if (current.iInnerPoly.Value + 1 < poly.InteriorRings.Count)
                    {
                        int iNextInner = current.iInnerPoly.Value + 1;
                        //Move to the next inner polygon
                        return new PolygonIndex(current.iPoly, iNextInner, 0, poly.InteriorRings.ElementAt(iNextInner).Length - 1);
                    }
                    else
                    {
                        //No more polygons, move to the next polygon, handled below
                    }
                }
                else
                {
                    if (poly.HasInteriorRings)
                    {
                        return new PolygonIndex(current.iPoly, 0, 0, poly.InteriorRings[0].Length - 1); //Go to the first vertex of the first inner polygon
                    }
                }

                //OK, we need to move on and could not move to an inner ring.  Go to the next polygon

                int iNextPoly = iPoly + 1;
                if (iNextPoly >= inputPolygons.Count)
                {
                    return new PolygonIndex?();
                }
                else
                {
                    return new Geometry.PolygonIndex(current.iPoly + 1, 0, inputPolygons[iNextPoly].ExteriorRing.Length - 1);
                }
            }
        }

        public void Reset()
        {
            curIndex = new PolygonIndex?();
        }

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator<PolygonIndex>)this;
        }

        IEnumerator<PolygonIndex> IEnumerable<PolygonIndex>.GetEnumerator()
        {
            return (IEnumerator<PolygonIndex>)this;
        }
    }
}
