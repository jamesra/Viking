using System;
using System.Collections.Generic;

namespace Geometry
{
    public readonly struct Coord(int ix, int iy) : IComparer<Coord>, IComparable<Coord>
    {
        public readonly int iX = ix;
        public readonly int iY = iy;

        public override string ToString() => iX.ToString() + "," + iY.ToString();

        public override bool Equals(object obj)
        {
            if (obj is Coord coord)
                return this == coord;

            return false;
        }

        public override int GetHashCode() => iX * iY;

        public static bool operator ==(Coord A, Coord B) => ((A.iX == B.iX) && (A.iY == B.iY));

        public static bool operator !=(Coord A, Coord B) => !((A.iX == B.iX) && (A.iY == B.iY));

        public int Compare(Coord x, Coord y)
        {
            int diff = x.iX - y.iX;
            if (diff == 0)
            {
                diff = x.iY - y.iY;
            }

            return diff;
        }

        public int CompareTo(Coord other) => Compare(this, other);
    }
}
