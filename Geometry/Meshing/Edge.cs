using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Geometry.Meshing
{
    /// <summary>
    /// Hash mixing for mesh keys. The earlier hashes multiplied the vertex indices, so every edge touching vertex 0
    /// collided and index pairs with the same product (3*8 and 4*6) collided as well.
    /// </summary>
    /// <remarks>
    /// Nothing in the mesh code orders output by these hashes: <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>
    /// and <see cref="System.Collections.Generic.HashSet{T}"/> enumerate in insertion and removal order, never in hash order,
    /// and no ConcurrentDictionary, Hashtable or immutable hash collection is keyed on an edge or a face.
    /// Keep it that way: an enumeration that depended on hash order would change with this function.
    /// </remarks>
    internal static class MeshHash
    {
        /// <summary>
        /// Hash of two vertex indices. Callers pass them in the same order for equal keys (smaller index first).
        /// </summary>
        /// <returns>A non-negative value, like the A*B hash it replaces.</returns>
        public static int Pair(int a, int b)
        {
            unchecked
            {
                ulong x = ((ulong)(uint)a << 32) | (uint)b;
                return Finish(x);
            }
        }

        /// <summary>
        /// Hash of three vertex indices in ascending order, used for triangle faces.
        /// </summary>
        public static int Triple(int a, int b, int c)
        {
            unchecked
            {
                ulong x = (((ulong)(uint)a << 32) | (uint)b) ^ ((ulong)(uint)c * 0x9E3779B97F4A7C15UL);
                return Finish(x);
            }
        }

        private static int Finish(ulong x)
        {
            unchecked
            {
                x ^= x >> 33;
                x *= 0xFF51AFD7ED558CCDUL;
                x ^= x >> 33;
                x *= 0xC4CEB9FE1A85EC53UL;
                x ^= x >> 33;
                return (int)(x & int.MaxValue);
            }
        }
    }

    public readonly struct EdgeKey : IComparable<EdgeKey>, IEquatable<EdgeKey>, IComparable<IEdgeKey>, IEquatable<IEdgeKey>, IEdgeKey
    {
        public int[] Vertices => [A, B]; //The two verticies defining the edge

        public int A
        {
            get;
        }

        public int B
        {
            get;
        }

        /// <summary>
        /// Creates the key with the smaller index in <see cref="A"/>, so (3, 7) and (7, 3) are the same key.
        /// </summary>
        public EdgeKey(int a, int b)
        {
            if (a < b)
            {
                this.A = a;
                this.B = b;
            }
            else
            {
                this.A = b;
                this.B = a;
            }
        }

        public EdgeKey(long a, long b) : this((int)a, (int)b)
        {

        }

        public static bool operator ==(EdgeKey A, EdgeKey B) => A.Equals(B);

        public static bool operator !=(EdgeKey A, EdgeKey B) => !A.Equals(B);

        public static bool operator ==(EdgeKey A, IEdgeKey B) => A.Equals(B);

        public static bool operator !=(EdgeKey A, IEdgeKey B) => !A.Equals(B);

        public int CompareTo(EdgeKey other)
        {
            int comparison = this.A - other.A;
            if (comparison == 0)
            {
                return this.B - other.B;
            }

            return comparison;
        }

        public int CompareTo(IEdgeKey other)
        {
            int comparison = this.A - other.A;
            if (comparison == 0)
            {
                return this.B - other.B;
            }

            return comparison;
        }

        public bool Equals(EdgeKey other) => this.A == other.A && this.B == other.B;

        public bool Equals(IEdgeKey other)
        {
            if (other is null)
                return false;

            return this.A == other.A && this.B == other.B;
        }

        public override bool Equals(object obj)
        {
            if (obj is EdgeKey key)
                return Equals(key);
            if (obj is IEdgeKey iKey)
                return Equals(iKey);

            return false;
        }

        /// <summary>
        /// Shares <see cref="MeshHash.Pair"/> with <see cref="EndpointPair"/> and <see cref="Edge"/>, so all three give one hash
        /// for the same two vertices. Always non-negative.
        /// </summary>
        public override int GetHashCode() => MeshHash.Pair(A, B);

        public override string ToString() => $"{A}-{B}";

        public int OppositeEnd(int value)
        {
            if (value == this.A)
                return B;
            else if (value == this.B)
                return A;
            else
            {
                throw new ArgumentException("Parameter to OppositeEnd must match one of the ends of the edge");
            }
        }

        public long OppositeEnd(long value)
        {
            if (value == this.A)
                return B;
            else if (value == this.B)
                return A;
            else
            {
                throw new ArgumentException("Parameter to OppositeEnd must match one of the ends of the edge");
            }
        }

        public bool Contains(long vertex) => this.A == vertex || this.B == vertex;

        /// <summary>
        /// True if the edges share a vertex, but are not identical
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Adjacent(IEdgeKey key)
        {
            return (this.A == key.A && this.B != key.B) ||
                   (this.A == key.B && this.B != key.A) ||
                   (this.B == key.A && this.A != key.B) ||
                   (this.B == key.B && this.A != key.A);
        }
    }

    public class Edge : IComparable<IEdge>, IEquatable<IEdge>, IEdge
    {
        protected readonly SortedSet<IFace> _Faces; //The two faces adjacent to the edge
        public readonly IEdgeKey Key;

        public int A => Key.A;

        public int B => Key.B;

        IEdgeKey IEdge.Key => this.Key;

        private ImmutableSortedSet<IFace> _ImmutableFaces;

        public ImmutableSortedSet<IFace> Faces
        {
            get
            {
                _ImmutableFaces ??= [.. _Faces];

                return this._ImmutableFaces;
            }
        }

        public static IEdge Create(int A, int B) => new Edge(A, B);

        /// <summary>
        /// Duplicate functions are used to create a copy of the edge, with index numbers adjusted by the offset, without any face data.
        /// This method is used to merge meshes
        /// </summary>
        /// <param name="oldVertex"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static Edge CreateOffsetCopy(Edge oldEdge, int offset)
        {
            Edge newEdge = new(oldEdge.A + offset, oldEdge.B + offset);
            return newEdge;
        }

        public static IEdge CreateOffsetCopy(IEdge oldEdge, int A, int B)
        {
            Edge newEdge = new(A, B);
            return newEdge;
        }

        public Edge(int a, int b)
        {
            if (a == b)
                throw new ArgumentException("Edges cannot have the same start and end point");

            _Faces = [];
            _ImmutableFaces = null;
            Key = new EdgeKey(a, b);
        }

        public Edge(EdgeKey key)
        {
            _Faces = [];
            _ImmutableFaces = null;
            Key = key;
        }

        public Edge(IEdgeKey key)
        {
            _Faces = [];
            _ImmutableFaces = null;
            Key = key;
        }

        public IEdge DeepCopyWithOffset(int VertexIndexOffset)
        {
            Edge e = new(A + VertexIndexOffset, B + VertexIndexOffset);

            return e;
        }

        public virtual void AddFace(IFace f)
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

        /// <summary>
        /// Return the face that is not the passed face
        /// Throw an exception if the edge is not part of the passed face
        /// Return null if the edge only has a single face
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public IFace OppositeFace(IFace f)
        {
            if (!this.Faces.Contains(f))
                throw new ArgumentException(string.Format("{0} is not part of face {1} and cannot return the opposite face", this.ToString(), f.ToString()));

            if (this.Faces.Count == 1)
                return null;

            return this.Faces.First(face => face.Equals(f) == false);
        }

        public static bool operator ==(Edge A, Edge B)
        {
            if (A is null)
                return B is null;

            return A.Equals(B);
        }

        public static bool operator !=(Edge A, Edge B)
        {
            if (A is null)
                return B is not null;

            return !A.Equals(B);
        }

        public static bool operator ==(Edge A, IEdge B)
        {
            if (A is null)
                return B is null;

            return A.Equals(B);
        }

        public static bool operator !=(Edge A, IEdge B)
        {
            if (A is null)
                return B is not null;

            return !A.Equals(B);
        }

        public int CompareTo(Edge other) => this.Key.CompareTo(other.Key);

        public int CompareTo(IEdge other) => this.Key.CompareTo(other.Key);

        public int CompareTo(IEdgeKey other) => this.Key.CompareTo(other);

        public bool Equals(Edge other)
        {
            if (other is null)
                return false;

            return this.Key.Equals(other.Key);
        }

        public bool Equals(IEdge other)
        {
            if (other is null)
                return false;

            return this.Key.Equals(other.Key);
        }

        public bool Equals(IEdgeKey other)
        {
            if (other is null)
                return false;

            return this.Key.Equals(other);
        }

        public override bool Equals(object obj)
        {
            if (obj is Edge other)
                return Equals(other);
            if (obj is IEdge iOther)
                return Equals(iOther);
            if (obj is IEdgeKey iKey)
                return Equals(iKey);

            return false;
        }

        public override int GetHashCode() => Key.GetHashCode();

        public override string ToString() => Key.ToString();

        public int OppositeEnd(int end)
        {
            Debug.Assert(this.A == end || this.B == end);

            return end == A ? B : A;
        }

        public long OppositeEnd(long end)
        {
            Debug.Assert(this.A == end || this.B == end);

            return end == A ? B : A;
        }

        public bool Contains(int endpoint) => endpoint == A || endpoint == B;

        public bool Contains(long endpoint) => endpoint == A || endpoint == B;

        /// <summary>
        /// True if the edges share a vertex, but are not identical
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Adjacent(IEdgeKey key)
        {
            return (this.A == key.A && this.B != key.B) ||
                   (this.A == key.B && this.B != key.A) ||
                   (this.B == key.A && this.A != key.B) ||
                   (this.B == key.B && this.A != key.A);
        }

        public virtual IEdge Clone()
        {
            Edge e = new(this.Key)
            {
                _ImmutableFaces = [.. this.Faces.Select(f => f.Clone())]
            };
            return e;
        }

        /// <summary>
        /// Assuming a 2D mesh or an edge with no more than 2 faces attached, this function returns a CCW list of the verticies that form the faces this edge contributes to.
        /// In the case of a triangulation, this will return a quadrilateral.
        /// 
        /// The beginning and end are not duplicates
        /// </summary>
        /// <returns></returns>
        public int[] FacesBoundary()
        {
            Debug.Assert(this.Faces.Count == 2, "Expected a triangulation when I wrote this function, future uses may render this assert meaningless");
            if (this.Faces.Count == 0)
            {
                return [];
            }
            else if (this.Faces.Count == 1)
            {
                return [.. this.Faces[0].iVerts];
            }
            else if (this.Faces.Count > 2)
            {
                //This can never work
                throw new ArgumentException("Cannot return counter-clockwise ordered boundary verts of an edge with three faces");
            }

            IFace FaceA = Faces[0] as IFace;
            IFace FaceB = Faces[1] as IFace;

            IIndexSet IndexerA = new InfiniteIndexSet(FaceA.iVerts, FaceA.iVerts.IndexOf(this.A));
            IIndexSet IndexerB;
            if (IndexerA[1] == B)
            {
                IndexerA = IndexerA.Reverse();
                IndexerB = new InfiniteIndexSet(FaceB.iVerts, FaceB.iVerts.IndexOf(this.A));
            }
            else
            {
                IndexerB = new InfiniteIndexSet(FaceB.iVerts, FaceB.iVerts.IndexOf(this.B));
            }

            List<long> boundary = new(FaceA.iVerts.Length + FaceB.iVerts.Length);
            boundary.AddRange(IndexerA);
            boundary.AddRange(IndexerB);

            //Remove the duplicate in the middle and end
            boundary.RemoveAt(boundary.Count - 1);
            boundary.RemoveAt(IndexerA.Count);

            return [.. boundary.Select(i => (int)i)];
        }
    }
}
