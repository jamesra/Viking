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
        readonly Polygon polygon;

        /// <summary>
        /// If set indicies returned by this enumerator will use this value for the iPoly field of the polygon index
        /// </summary>
        public int? PolyIndex = new int?();

        public bool Reverse = false;

        public PolygonVertexEnum(Polygon poly, bool reverse = false)
        {
            this.polygon = poly;
            curIndex = new Geometry.PolygonIndex?();
            Reverse = reverse;
        }

        public PolygonVertexEnum(Polygon poly, int ForceiPoly, bool reverse = false)
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

        public void Dispose() => GC.SuppressFinalize(this);

        /// <summary>
        /// Go to the next index, if the shape is closed we do not return the closed index twice. 
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            if (!curIndex.HasValue)
            {
                if (polygon is null)
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

        private static PolygonIndex? PrevIndex(Polygon poly, PolygonIndex current)
        {
            int iPrevIndex = current.VertexIndex - 1;

            if (iPrevIndex >= 0) //We still have verticies on our current ring, so move back one step
            {
                //Move along the ring we are iterating
                return new PolygonIndex(current.ShapeIndex, current.InnerShapeIndex, iPrevIndex, current.NumUniqueInRing);
            }
            else
            //OK, handle case where we are out of indicies on the current ring            
            {
                //Find the next ring
                if (current.IsInner)
                {
                    int iPrevInner = current.InnerShapeIndex.Value - 1;
                    if (iPrevInner >= 0)
                    {

                        //Move to the previous inner polygon
                        return new PolygonIndex(current.ShapeIndex, iPrevInner, poly.InteriorRings[iPrevInner].Length - 2, poly.InteriorRings[iPrevInner].Length - 1);
                    }
                    else
                    {
                        //No more polygons, move to the exterior polygon, handled below
                        return new PolygonIndex(current.ShapeIndex, poly.ExteriorRing.Length - 2, poly.ExteriorRing.Length - 1); //Go to the last vertex of the exterior ring
                    }
                }
                else
                {
                    //OK, we finished enumerating the exterior ring.  Normally this is where we go to the previous polygon but since this enumerator only covers a single polygon we are done.
                    return new PolygonIndex?();
                }
            }
        }

        private static PolygonIndex? NextIndex(Polygon poly, PolygonIndex current)
        {
            int iNextVert = current.VertexIndex + 1;

            if (iNextVert < current.NumUniqueInRing) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Move along the ring we are iterating
                return new PolygonIndex(current.ShapeIndex, current.InnerShapeIndex, iNextVert, current.NumUniqueInRing);
            }

            if (iNextVert == current.NumUniqueInRing) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                Vector2[] ring = current.GetRing(poly);
                //Check for the case where the final vertex in the ring is not equal to the first.
                if (ring[0] != ring[iNextVert])
                    return new PolygonIndex(current.ShapeIndex, current.InnerShapeIndex, iNextVert, current.NumUniqueInRing);
            }

            //OK, handle case where we are out of indicies on the current ring            
            {
                //Find the next ring
                if (current.IsInner)
                {
                    if (current.InnerShapeIndex.Value + 1 < poly.InteriorRings.Count)
                    {
                        int iNextInner = current.InnerShapeIndex.Value + 1;
                        //Move to the next inner polygon
                        return new PolygonIndex(current.ShapeIndex, iNextInner, 0, poly.InteriorRings.ElementAt(iNextInner).Length - 1);
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
                        return new PolygonIndex(current.ShapeIndex, 0, 0, poly.InteriorRings[0].Length - 1); //Go to the first vertex of the first inner polygon
                    }
                }

                //OK, we need to move on and could not move to an inner ring.  Normally this is where we go to the next polygon but since this enumerator only covers a single polygon we are done.
                return new PolygonIndex?();
            }
        }

        public void Reset() => curIndex = new PolygonIndex?();

        public IEnumerator GetEnumerator() => (IEnumerator<PolygonIndex>)this;

        IEnumerator<PolygonIndex> IEnumerable<PolygonIndex>.GetEnumerator() => (IEnumerator<PolygonIndex>)this;
    }
}
