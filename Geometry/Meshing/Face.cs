using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    public struct Face : IComparable<Face>, IEquatable<Face>
    {
        public readonly int[] iVerts;

        public int A
        {
            get { return iVerts[0]; }
        }

        public int B
        {
            get { return iVerts[1]; }
        }

        public int C
        {
            get { return iVerts[2]; }
        }

        public Edge AB
        {
            get { return new Edge(A, B); }
        }

        public Edge BC
        {
            get { return new Edge(B,C); }
        }
        public Edge CA
        {
            get { return new Edge(C,A); }
        }

        public Edge[] Edges
        {
            get { return new Edge[] { AB, BC, CA }; }
        }

        public Face(int A, int B, int C)
        {
            SortedSet<int> vertList = new SortedSet<int>(new int[] { A, B, C }); 
            iVerts = vertList.ToArray(); 
        }

        public Face(IEnumerable<int> vertex_indicies)
        {
            SortedSet<int> vertList = new SortedSet<int>(vertex_indicies); 
            iVerts = vertList.ToArray();
        }

        public override int GetHashCode()
        {
            return System.Convert.ToInt32(((long)A * (long)B * (long)C));
        }

        public override string ToString()
        {
            return string.Format("{0},{1},{2}", iVerts[0], iVerts[1], iVerts[2]);
        }

        public static bool operator ==(Face A, Face B)
        {
            return A.Equals(B);
        }

        public static bool operator !=(Face A, Face B)
        {
            return !A.Equals(B);
        }

        public override bool Equals(object obj)
        {
            Face E = (Face)obj;
            if (object.ReferenceEquals(E, null))
            {
                return false;
            }

            return this.Equals(E);
        }

        public int CompareTo(Face other)
        {
            for (int i = 0; i < iVerts.Length; i++)
            {
                int compareVal = this.iVerts[i].CompareTo(other.iVerts[i]);
                if (compareVal != 0)
                    return compareVal;
            }

            return 0;
        }

        public bool Equals(Face other)
        {
            if (other.iVerts.Length != this.iVerts.Length)
                return false;

            for (int i = 0; i < iVerts.Length; i++)
            {
                if (this.iVerts[i] != other.iVerts[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
