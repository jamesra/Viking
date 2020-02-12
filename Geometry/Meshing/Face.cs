using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    /// <summary>
    /// A face in a mesh.  Should be perimeter around the face in order.  Each consecutive index is connected by an edge
    /// </summary>
    public class Face : IComparable<Face>, IEquatable<Face>, IFace
    {
        private readonly ImmutableArray<int> _iVerts;
        private readonly ImmutableArray<IEdgeKey> _Edges; 

        public ImmutableArray<int> iVerts
        {
            get
            {
                return this._iVerts;
            }
        }

        public ImmutableArray<IEdgeKey> Edges
        {
            get
            {
                return this._Edges;
            }
        }

          
        private IEdgeKey[] CalculateEdges()
        {
            IEdgeKey[] _edges = new IEdgeKey[iVerts.Length];
            for (int i = 0; i < iVerts.Length; i++)
            {
                if (i < iVerts.Length - 1)
                    _edges[i] = new EdgeKey(iVerts[i], iVerts[i + 1]);
                else
                    _edges[i] = new EdgeKey(iVerts[i], iVerts[0]);
            }

            return _edges;
        }

        public static IFace Create(IEnumerable<int> vertex_indicies)
        {
            return new Face(vertex_indicies);
        }

        /// <summary>
        /// Duplicate functions are used to create a copy of the face, with index numbers adjusted by the offset, without any edge data.
        /// This method is used to merge meshes
        /// </summary>
        /// <param name="oldVertex"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static Face CreateOffsetCopy(Face oldFace, int offset)
        {
            Face newFace = new Meshing.Face(oldFace.iVerts.Select(VertIndex => VertIndex + offset));
            return newFace;
        }

        public static Face CreateOffsetCopy(Face oldFace, IEnumerable<int> vertex_indicies)
        {
            Face newFace = new Meshing.Face(vertex_indicies);
            return newFace;
        }

        public static IFace CreateOffsetCopy(IFace oldFace, IEnumerable<int> vertex_indicies)
        {
            Face newFace = new Meshing.Face(vertex_indicies);
            return newFace;
        }

        public Face(int A, int B, int C)
        {
            if(A == B || A == C || B == C)
            {
                throw new ArgumentException("Vertex indicies must be unique");
            }
            _iVerts = (new int[] { A, B, C}).ToImmutableArray();
            _Edges = CalculateEdges().ToImmutableArray();
        }

        public Face(int A, int B, int C, int D)
        {
            if (A == B || A == C || A == D || 
                B == C || B == D ||
                C == D)
            {
                throw new ArgumentException("Vertex indicies must be unique");
            }

            _iVerts = (new int[] { A, B, C, D }).ToImmutableArray();
            _Edges = CalculateEdges().ToImmutableArray();
        }

        /// <summary>
        /// Used only for creating copies
        /// </summary>
        /// <param name="vertex_indicies"></param>
        /// <param name="edges"></param>
        protected Face(IEnumerable<int> vertex_indicies, IEnumerable<IEdgeKey> edges) : this(vertex_indicies)
        {
            this._Edges = edges.ToImmutableArray();
        }


        public Face(IEnumerable<int> vertex_indicies)
        {
            _iVerts = vertex_indicies.ToImmutableArray();
            SortedSet<int> s = new SortedSet<int>(iVerts);
            if(s.Count != iVerts.Length)
            {
                throw new ArgumentException("Vertex indicies must be unique");
            }

            if (iVerts.Length < 3 || iVerts.Length > 4)
                throw new ArgumentException("A face must have at least 3 verticies and currently no more than 4.  The 4 limit is negiotiable.");

            _Edges = CalculateEdges().ToImmutableArray();
        }

        /// <summary>
        /// Returns the edges this face and another face have in common
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public IEdgeKey[] SharedEdges(Face other)
        {
            IEdgeKey[] shared = this.Edges.Intersect(other.Edges).ToArray();
            return shared;
        }

        public override int GetHashCode()
        {
            return iVerts.Sum();
        }

        public override string ToString()
        {
            return string.Join(",", this.iVerts);
        }

        /// <summary>
        /// Reverse the order of verticies to flip the orientation of the face
        /// </summary>
        /// <returns></returns>
        public Face Flip()
        {
            return new Face(this.iVerts.Reverse());
        }

        public static bool operator ==(Face A, Face B)
        {
            if (object.ReferenceEquals(A, B))
                return true; 

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

        public bool IsTriangle
        {
            get
            {
                return iVerts.Length == 3;
            }
        }

        public bool IsQuad
        {
            get
            {
                return iVerts.Length == 4;
            }
        }
          
        public int CompareTo(Face other)
        {
            return CompareTo(other as IFace);
        }

        public int CompareTo(IFace other)
        {
            int compareVal = this.iVerts.Length.CompareTo(other.iVerts.Length);
            if (compareVal != 0)
                return compareVal;

            
            ImmutableArray<int> A = this.iVerts.Sort();
            ImmutableArray<int> B = other.iVerts.Sort();

            for (int i = 0; i < iVerts.Length; i++)
            {
                compareVal = A[i].CompareTo(B[i]);
                if (compareVal != 0)
                    return compareVal;
            }

            return 0;
        }

        public bool Equals(Face other)
        {
            return Equals(other as IFace);
        }
         
        /// <summary>
        /// Equals ignores clockwise or counter-clockwise at this time
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(IFace other)
        {
            if (object.ReferenceEquals(other, null))
            {
                return false;
            }

            if (other.iVerts.Length != this.iVerts.Length)
                return false;

            SortedSet<int> A = new SortedSet<int>(this.iVerts);
            SortedSet<int> B = new SortedSet<int>(other.iVerts);

            return A.SetEquals(B);
        }

        public virtual IFace Clone()
        {
            var f = new Face(this.iVerts, this.Edges);
            return f;
        }
    }
}
