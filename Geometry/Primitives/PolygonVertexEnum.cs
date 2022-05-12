using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Enumerates verticies of a polygon, starting with verticies on the exterior ring and then continuing to verticies on any interior rings
    /// </summary>
    public class PolygonVertexEnum : IEnumerator<PolygonIndex>, IEnumerator, IEnumerable<PolygonIndex>, IEnumerable
    {
        PolygonIndex? curIndex;
        readonly GridPolygon polygon;

        /// <summary>
        /// If set indicies returned by this enumerator will use this value for the iPoly field of the polygon index
        /// </summary>
        public int? PolyIndex = new int?();

        public bool Reverse = false;

        public PolygonVertexEnum(GridPolygon poly, bool reverse = false)
        {
            this.polygon = poly;
            curIndex = new Geometry.PolygonIndex?();
            Reverse = reverse;
        }

        public PolygonVertexEnum(GridPolygon poly, int ForceiPoly, bool reverse = false)
        {
            this.polygon = poly;
            curIndex = new Geometry.PolygonIndex?();
            PolyIndex = ForceiPoly;
            Reverse = reverse;
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
                if (polygon == null)
                    return false;

                if (polygon.ExteriorRing.Length == 0)
                    return false;

                if (false == Reverse)
                {
                    curIndex = new PolygonIndex(PolyIndex ?? 0, 0, polygon.ExteriorRing.Length - 1);
                    return true;
                }
                else
                {
                    if (polygon.HasInteriorRings)
                    {
                        int innerIndex = polygon.InteriorRings.Count - 1;
                        var interiorRing = polygon.InteriorRings[innerIndex];
                        curIndex = new PolygonIndex(PolyIndex ?? 0, innerIndex, interiorRing.Length - 1, interiorRing.Length - 1);
                    }
                    else
                    {
                        curIndex = new PolygonIndex(PolyIndex ?? 0, polygon.ExteriorRing.Length - 1, polygon.ExteriorRing.Length - 1);
                    }
                }
            }

            curIndex = Reverse ? PrevIndex(polygon, curIndex.Value) : NextIndex(polygon, curIndex.Value);

            return curIndex.HasValue;
        }

        private static PolygonIndex? PrevIndex(GridPolygon poly, PolygonIndex current)
        {
            int iPrevIndex = current.iVertex - 1;

            if (iPrevIndex >= 0) //We still have verticies on our current ring, so move back one step
            {
                //Move along the ring we are iterating
                return new PolygonIndex(current.iPoly, current.iInnerPoly, iPrevIndex, current.NumUniqueInRing);
            }
            else
            //OK, handle case where we are out of indicies on the current ring            
            {
                //Find the next ring
                if (current.IsInner)
                {
                    int iPrevInner = current.iInnerPoly.Value - 1;
                    if (iPrevInner >= 0)
                    {

                        //Move to the previous inner polygon
                        return new PolygonIndex(current.iPoly, iPrevInner, poly.InteriorRings[iPrevInner].Length - 2, poly.InteriorRings[iPrevInner].Length - 1);
                    }
                    else
                    {
                        //No more polygons, move to the exterior polygon, handled below
                        return new PolygonIndex(current.iPoly, poly.ExteriorRing.Length - 2, poly.ExteriorRing.Length - 1); //Go to the last vertex of the exterior ring
                    }
                }
                else
                {
                    //OK, we finished enumerating the exterior ring.  Normally this is where we go to the previous polygon but since this enumerator only covers a single polygon we are done.
                    return new PolygonIndex?();
                }
            }
        }

        private static PolygonIndex? NextIndex(GridPolygon poly, PolygonIndex current)
        {
            int iNextVert = current.iVertex + 1;

            if (iNextVert < current.NumUniqueInRing) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Move along the ring we are iterating
                return new PolygonIndex(current.iPoly, current.iInnerPoly, iNextVert, current.NumUniqueInRing);
            }

            if (iNextVert == current.NumUniqueInRing) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                GridVector2[] ring = current.GetRing(poly);
                //Check for the case where the final vertex in the ring is not equal to the first.
                if (ring[0] != ring[iNextVert])
                    return new PolygonIndex(current.iPoly, current.iInnerPoly, iNextVert, current.NumUniqueInRing);
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

                //OK, we need to move on and could not move to an inner ring.  Normally this is where we go to the next polygon but since this enumerator only covers a single polygon we are done.
                return new PolygonIndex?();
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
