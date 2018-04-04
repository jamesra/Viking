using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    public struct EdgeKey : IComparable<EdgeKey>, IEquatable<EdgeKey>, IComparable<IEdgeKey>, IEquatable<IEdgeKey>, IEdgeKey
    {
        public int[] Verticies //The two verticies defining the edge
        {
            get { return new int[] { A, B }; }
        }
                
        public int A
        {
            get;
            private set;
        }

        public int B
        {
            get;
            private set;
        }

        public EdgeKey(int a, int b)
        { 
            int[] ordered = a < b ? new int[] { a, b } : new int[] { b, a };
            this.A = ordered[0];
            this.B = ordered[1];
        }

        public static bool operator ==(EdgeKey A, EdgeKey B)
        {
            return A.Equals(B);
        }

        public static bool operator !=(EdgeKey A, EdgeKey B)
        {
            return !A.Equals(B);
        }

        public static bool operator ==(EdgeKey A, IEdgeKey B)
        {
            return A.Equals(B);
        }

        public static bool operator !=(EdgeKey A, IEdgeKey B)
        {
            return !A.Equals(B);
        }

        public int CompareTo(EdgeKey other)
        {
            for(int i = 0; i < Verticies.Length; i++)
            {
                int comparison = this.Verticies[i].CompareTo(other.Verticies[i]);
                if (comparison != 0)
                    return comparison;
            }

            return 0;
        }

        public int CompareTo(IEdgeKey other)
        {
            int comparison = this.A.CompareTo(other.A);
            if(comparison == 0)
            {
                comparison = this.B.CompareTo(other.B); 
            }

            return comparison;
        }

        public bool Equals(EdgeKey other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            return this.A == other.A && this.B == other.B;
        }

        public bool Equals(IEdgeKey other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            return this.A == other.A && this.B == other.B;
        }

        public override bool Equals(object obj)
        {
            IEdgeKey E = (IEdgeKey)obj;
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

        public override string ToString()
        {
            return string.Format("{0}-{1}", A, B);
        }
    }

    public class Edge : IComparable<IEdge>, IEquatable<IEdge>, IEdge
    {
        readonly protected SortedSet<IFace> _Faces; //The two faces adjacent to the edge
        readonly public IEdgeKey Key;

        public int A
        {
            get { return Key.A; }
        }

        public int B
        {
            get { return Key.B; }
        }
         
        IEdgeKey IEdge.Key
        {
            get { return this.Key; }
        }

        private ImmutableSortedSet<IFace> _ImmutableFaces; 

        ImmutableSortedSet<IFace> IEdge.Faces
        {
            get
            {
                if(_ImmutableFaces == null)
                {
                    _ImmutableFaces = _Faces.ToImmutableSortedSet();
                }

                return this._ImmutableFaces;
            }
        }

        /// <summary>
        /// Duplicate functions are used to create a copy of the edge, with index numbers adjusted by the offset, without any face data.
        /// This method is used to merge meshes
        /// </summary>
        /// <param name="oldVertex"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static Edge Duplicate(Edge oldEdge, int offset)
        {
            Edge newEdge = new Meshing.Edge(oldEdge.A + offset, oldEdge.B + offset);
            return newEdge;
        }

        public static IEdge Duplicate(IEdge oldEdge, int A, int B)
        {
            Edge newEdge = new Meshing.Edge(A,B);
            return newEdge;
        }

        public Edge(int a, int b)
        {
            if (a == b)
                throw new ArgumentException("Edges cannot have the same start and end point");

            _Faces = new SortedSet<IFace>();
            _ImmutableFaces = null;
            Key = new EdgeKey(a, b);
        }

        public Edge(EdgeKey key)
        {
            _Faces = new SortedSet<IFace>();
            _ImmutableFaces = null;
            Key = key;
        }

        public Edge(IEdgeKey key)
        {
            _Faces = new SortedSet<IFace>();
            _ImmutableFaces = null;
            Key = key;
        }

        public IEdge DeepCopyWithOffset(int VertexIndexOffset)
        {
            Edge e = new Meshing.Edge(A + VertexIndexOffset, B + VertexIndexOffset);

            return e;
        }

        public void AddFace(IFace f)
        {
            //Debug.Assert(Faces.Contains(f) == false);
            _Faces.Add(f);
            _ImmutableFaces = null;
        }

        public void RemoveFace(IFace f)
        {
            Debug.Assert(_Faces.Contains(f));
            _Faces.Remove(f);
            _ImmutableFaces = null;
        }

        public static bool operator ==(Edge A, Edge B)
        {
            if (object.ReferenceEquals(A, null))
            {
                return object.ReferenceEquals(B, null);
            }

            return A.Equals(B);
        } 

        public static bool operator !=(Edge A, Edge B)
        {
            if (object.ReferenceEquals(A, null))
            {
                return !object.ReferenceEquals(B, null);
            }

            return !A.Equals(B);
        }

        public static bool operator ==(Edge A, IEdge B)
        {
            if (object.ReferenceEquals(A,null))
            {
                return object.ReferenceEquals(B, null);
            }

            return A.Equals(B);
        }

        public static bool operator !=(Edge A, IEdge B)
        {
            if (object.ReferenceEquals(A, null))
            {
                return !object.ReferenceEquals(B, null);
            }

            return !A.Equals(B);
        }

        public int CompareTo(Edge other)
        {
            return this.Key.CompareTo(other.Key);
        }

        public int CompareTo(IEdge other)
        {
            return this.Key.CompareTo(other.Key);
        }

        public int CompareTo(IEdgeKey other)
        {
            return this.Key.CompareTo(other);
        }

        public bool Equals(Edge other)
        {
            if(object.ReferenceEquals(null, other))
            {
                return false; 
            }

            return this.Key.Equals(other.Key);
        }

        public bool Equals(IEdge other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            return this.Key.Equals(other.Key);
        }

        public bool Equals(IEdgeKey other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            return this.Key.Equals(other);
        }
           
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null) || GetType() != obj.GetType())
            {
                return false;
            }

            Edge E = obj as Edge;
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

        public override string ToString()
        {
            return Key.ToString();
        }

        public int OppositeEnd(int end)
        {
            Debug.Assert(this.A == end || this.B == end);

            return end == A ? B : A;
        }

        public bool Contains(int endpoint)
        {
            return endpoint == A || endpoint == B;
        }
    }
}
