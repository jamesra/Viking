using System;
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

        /*
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

        */
          
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

        /// <summary>
        /// Duplicate functions are used to create a copy of the face, with index numbers adjusted by the offset, without any edge data.
        /// This method is used to merge meshes
        /// </summary>
        /// <param name="oldVertex"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static Face Duplicate(Face oldFace, int offset)
        {
            Face newFace = new Meshing.Face(oldFace.iVerts.Select(VertIndex => VertIndex + offset));
            return newFace;
        }

        public static Face Duplicate(Face oldFace, IEnumerable<int> vertex_indicies)
        {
            Face newFace = new Meshing.Face(vertex_indicies);
            return newFace;
        }

        public static IFace Duplicate(IFace oldFace, IEnumerable<int> vertex_indicies)
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

            for (int i = 0; i < iVerts.Length; i++)
            {
                compareVal = this.iVerts[i].CompareTo(other.iVerts[i]);
                if (compareVal != 0)
                    return compareVal;
            }

            return 0;
        }

        public bool Equals(Face other)
        {
            return Equals(other as IFace);
        }
         
        public bool Equals(IFace other)
        {
            if (object.ReferenceEquals(other, null))
            {
                return false;
            }

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
