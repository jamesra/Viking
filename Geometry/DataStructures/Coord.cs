using System;
using System.Collections.Generic;

namespace Geometry
{
    public readonly struct Coord : IComparer<Coord>, IComparable<Coord>
    {
        public readonly int iX;
        public readonly int iY;

        public Coord(int ix, int iy)
        {
            this.iX = ix;
            this.iY = iy;
        }

        public override string ToString()
        {
            return iX.ToString() + "," + iY.ToString();
        }

        public override bool Equals(object obj)
        {
            if(obj is Coord coord)
                return this == coord;

            return false;
        }

        public override int GetHashCode()
        {
            return iX * iY;
        }

        public static bool operator ==(Coord A, Coord B)
        {
            return ((A.iX == B.iX) && (A.iY == B.iY));
        }

        public static bool operator !=(Coord A, Coord B)
        {
            return !((A.iX == B.iX) && (A.iY == B.iY));
        }

        public int Compare(Coord x, Coord y)
        {
            int diff = x.iX - y.iX;
            if (diff == 0)
            {
                diff = x.iY - y.iY;
            }

            return diff;
        }

        public int CompareTo(Coord other)
        {
            return Compare(this, other);
        }
    }
}
