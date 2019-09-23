using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{ 
    public class Vertex<T> : Vertex
    {
        public T Data;

        public Vertex(GridVector3 p, GridVector3 n, T data) : base(p, n)
        {
            Data = data;
        }

        public Vertex(GridVector3 p, GridVector3 n) : base(p, n)
        {
        }

        public Vertex(GridVector3 p, T data) : base(p)
        {
            Data = data;
        }
    }

    public class Vertex : IVertex 
    {
        private GridVector3 _Position;
        private GridVector3 _Normal;
        private SortedSet<IEdgeKey> _Edges;
        public int Index { get;  set; }

        public GridVector3 Position
        {
            get
            {
                return _Position;
            }
            set
            {
                _Position = value;
            }
        }

        public GridVector3 Normal
        {
            get
            {
                return _Normal;
            }
            set
            {
                _Normal = value;
            }
        }

        private ImmutableSortedSet<IEdgeKey> _ImmutableEdges;
        public ImmutableSortedSet<IEdgeKey> Edges
        {
            get
            {
                if (_ImmutableEdges == null)
                {
                    _ImmutableEdges = _Edges.ToImmutableSortedSet();
                }
                return _ImmutableEdges;
            }
        }

        /// <summary>
        /// Duplicate functions are used to create a copy of the vertex without any edge or face data.
        /// </summary>
        /// <param name="oldVertex"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static Vertex CreateOffsetCopy(Vertex oldVertex, int offset)
        {
            Vertex newVertex = new Meshing.Vertex(oldVertex.Position, oldVertex.Normal);
            return newVertex;
        }

        public static IVertex CreateOffsetCopy(IVertex oldVertex, int offset)
        {
            Vertex newVertex = new Meshing.Vertex(oldVertex.Position, oldVertex.Normal);
            return newVertex;
        }

        public Vertex(GridVector3 p, GridVector3 n)
        {
            _Position = p;
            _Normal = n;
            _Edges = new SortedSet<IEdgeKey>();
            _ImmutableEdges = null;
        }

        public Vertex(GridVector3 p)
        {
            _Position = p;
            _Normal = GridVector3.Zero;
            _Edges = new SortedSet<IEdgeKey>();
            _ImmutableEdges = null;
        }

        public bool AddEdge(IEdgeKey e)
        {
            if (!_Edges.Contains(e))
            {
                _Edges.Add(e);
                _ImmutableEdges = null;

                return true;
            }

            return false;
        }

        public void RemoveEdge(IEdgeKey e)
        {
            Debug.Assert(_Edges.Contains(e));
            _Edges.Remove(e);
            _ImmutableEdges = null;
        }

        public override string ToString()
        {
            return string.Format("I: {0} P: {1} N: {2}", this.Index, Position, Normal);
        }

        public IVertex ShallowCopy()
        {
            Vertex v = new Vertex(this.Position, this.Normal);

            return v;
        }

        public int CompareTo(IVertex other)
        {
            return this.Index.CompareTo(other.Index);
        }

        public bool Equals(IVertex other)
        {
            return this.Index == other.Index;
        }
    }

}
