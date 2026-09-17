using System.Collections.Generic;
using System;
using System.Collections;

namespace Geometry
{
    /// <summary>
    /// Enumerates vertices of a polyline.
    /// </summary>
    public class PolylineVertexEnum : IEnumerator<PolylineIndex>, IEnumerator, IEnumerable<PolylineIndex>, IEnumerable
    {
    PolylineIndex? curIndex;
    readonly Polyline polyline;

    /// <summary>
    /// If set indicies returned by this enumerator will use this value for the iPoly field of the polyline index
    /// </summary>
    public int? PolyIndex = new int?();

    public bool Reverse = false;

    public PolylineVertexEnum(Polyline line, bool reverse = false)
    {
        this.polyline = line;
        curIndex = new Geometry.PolylineIndex?();
        Reverse = reverse;
    }

    public PolylineVertexEnum(Polyline line, int ForceiPoly, bool reverse = false)
    {
        this.polyline = line;
        curIndex = new Geometry.PolylineIndex?();
        PolyIndex = ForceiPoly;
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

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>
    /// Go to the next index, if the shape is closed we do not return the closed index twice. 
    /// </summary>
    /// <returns></returns>
    public bool MoveNext()
    {
        if (!curIndex.HasValue)
        {
            if (polyline is null)
                return false;

            if (polyline.NumUniqueVertices == 0)
                return false;

            if (false == Reverse)
            {
                curIndex = new PolylineIndex(PolyIndex ?? 0, 0, polyline.NumUniqueVertices);
                return true;
            }
            else
            {
                curIndex = new PolylineIndex(PolyIndex ?? 0, polyline.NumUniqueVertices - 1, polyline.NumUniqueVertices);
            }
        }

        curIndex = Reverse ? PrevIndex(polyline, curIndex.Value) : NextIndex(polyline, curIndex.Value);

        return curIndex.HasValue;
    }

    private static PolylineIndex? PrevIndex(Polyline poly, PolylineIndex current)
    {
        int iPrevIndex = current.VertexIndex - 1;

        if (iPrevIndex >= 0) //We still have verticies on our current ring, so move back one step
        {
            //Move along the ring we are iterating
            return new PolylineIndex(current.ShapeIndex, iPrevIndex, current.NumUnique);
        }
        else //We are out of indicies       
        {
            return new PolylineIndex?();
        }
    }

    private static PolylineIndex? NextIndex(Polyline poly, PolylineIndex current)
    {
        int iNextVert = current.VertexIndex + 1;

        if (iNextVert < current.NumUnique) //-1 because we do not want to report a duplicate vertex for a closed ring
        {
            //Move along the ring we are iterating
            return new PolylineIndex(current.ShapeIndex, iNextVert, current.NumUnique);
        }

        //We are out of indicies on the current ring    
        return new PolylineIndex?();
    }

    public void Reset() => curIndex = new PolylineIndex?();

    public IEnumerator GetEnumerator() => (IEnumerator<PolylineIndex>)this;

        IEnumerator<PolylineIndex> IEnumerable<PolylineIndex>.GetEnumerator() => (IEnumerator<PolylineIndex>)this;
    }
}