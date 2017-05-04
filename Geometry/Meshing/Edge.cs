using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    public struct EdgeKey : IComparable<EdgeKey>, IEquatable<EdgeKey>
    {
        readonly int[] Verticies; //The two verticies defining the edge

        public int A
        {
            get { return Verticies[0]; }
        }

        public int B
        {
            get { return Verticies[1]; }
        }

        public EdgeKey(int a, int b)
        { 
            Verticies = a < b ? new int[] { a, b } : new int[] { b, a };
        }

        public static bool operator ==(EdgeKey A, EdgeKey B)
        {
            return A.Equals(B);
        }

        public static bool operator !=(EdgeKey A, EdgeKey B)
        {
            return !A.Equals(B);
        }

        public int CompareTo(EdgeKey other)
        {
            if (this.A == other.A)
            {
                return this.B.CompareTo(other.B);
            }
            else
            {
                return this.A.CompareTo(other.A);
            }
        }

        public bool Equals(EdgeKey other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            return this.A == other.A && this.B == other.B;
        }

        public override bool Equals(object obj)
        {
            EdgeKey E = (EdgeKey)obj;
            if (object.ReferenceEquals(E, null))
            {
                return false;
            }

            return this.Equals(E);
        }

        public override int GetHashCode()
        {
            return System.Convert.ToInt32(((long)A * (long)B));
        }
    }

    public struct Edge : IComparable<Edge>, IEquatable<Edge>
    {
        readonly public SortedSet<Face> Faces; //The two faces adjacent to the edge
        readonly public EdgeKey Key;

        public int A
        {
            get { return Key.A; }
        }

        public int B
        {
            get { return Key.B; }
        }

        public Edge(int a, int b)
        {
            Faces = new SortedSet<Face>();
            Key = new EdgeKey(a, b);
        }

        public Edge(EdgeKey key)
        {
            Faces = new SortedSet<Face>();
            Key = key;
        }

        public void AddFace(Face f)
        {
            Debug.Assert(Faces.Contains(f) == false);
            Faces.Add(f); 
        }

        public void RemoveFace(Face f)
        {
            Debug.Assert(Faces.Contains(f));
            Faces.Remove(f);
        }

        public static bool operator ==(Edge A, Edge B)
        {
            return A.Equals(B);
        }

        public static bool operator !=(Edge A, Edge B)
        {
            return !A.Equals(B);
        }

        public int CompareTo(Edge other)
        {
            return this.Key.CompareTo(other.Key);
        }

        public bool Equals(Edge other)
        {
            if(object.ReferenceEquals(null, other))
            {
                return false; 
            }

            return this.Key.Equals(other.Key);
        }

        public override bool Equals(object obj)
        {
            Edge E = (Edge)obj;
            if (object.ReferenceEquals(E, null))
            {
                return false;
            }

            return this.Equals(E);
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

    }
}
