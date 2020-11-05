using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Geometry.Meshing
{
    /// <summary>
    /// A basic implementation of the IVertex class to inherit
    /// </summary>
    public abstract class VertexBase : IVertex, IComparable<VertexBase>, IEquatable<VertexBase>
    {
        public virtual int Index { get; set; }

        public IComparer<IEdgeKey> EdgeComparer
        {
            get
            {
                return _Edges.Comparer;
            }
            protected set
            {
                if (value != _Edges.Comparer)
                {
                    _Edges = new SortedSet<IEdgeKey>(_Edges, value);
                    _ImmutableEdges = null;
                }
            }
        }

        protected SortedSet<IEdgeKey> _Edges;

        private ImmutableSortedSet<IEdgeKey> _ImmutableEdges;
        public ImmutableSortedSet<IEdgeKey> Edges
        {
            get
            {
                if (_ImmutableEdges == null)
                {
                    _ImmutableEdges = _Edges.ToImmutableSortedSet(_Edges.Comparer);
                }
                return _ImmutableEdges;
            }
        }

        public VertexBase()
        {
            _Edges = new SortedSet<IEdgeKey>();
            _ImmutableEdges = null;
        }


        public VertexBase(IComparer<IEdgeKey> edgeComparer = null)
        {
            _Edges = new SortedSet<IEdgeKey>(edgeComparer);
            _ImmutableEdges = null;
        }

        public VertexBase(int index, IComparer<IEdgeKey> edgeComparer = null) : this(edgeComparer)
        {
            this.Index = index;
        }

        public virtual bool AddEdge(IEdgeKey e)
        {
            if (!_Edges.Contains(e))
            {
                _Edges.Add(e);
                _ImmutableEdges = null;

                return true;
            }

            return false;
        }

        public int CompareTo(IVertex other)
        {
            return this.Index.CompareTo(other.Index);
        }

        public int CompareTo(VertexBase other)
        {
            return this.Index.CompareTo(other.Index);
        }

        public bool Equals(IVertex other)
        {
            return this.Index == other.Index;
        }

        public bool Equals(VertexBase other)
        {
            return this.Index == other.Index;
        }

        public virtual void RemoveEdge(IEdgeKey e)
        {
            Debug.Assert(_Edges.Contains(e));
            _Edges.Remove(e);
            _ImmutableEdges = null;
        }

        public abstract IVertex ShallowCopy();
    }

    /// <summary>
    /// A templated vertex that stores additional data of type T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Vertex3D<T> : Vertex3D, IVertex3D<T>
    {
        public T Data { get; set; }

        public Vertex3D(GridVector3 p, GridVector3 n, T data) : base(p, n)
        {
            Data = data;
        }

        public Vertex3D(GridVector3 p, GridVector3 n) : base(p, n)
        {
        }

        public Vertex3D(GridVector3 p, T data) : base(p)
        {
            Data = data;
        }

        public override IVertex ShallowCopy()
        {
            Vertex3D<T> newVertex = new Vertex3D<T>(Position, Normal, Data);
            return newVertex;
        }
    }

    /// <summary>
    /// A basic 3D Vertex implementation
    /// </summary>
    public class Vertex3D : VertexBase, IVertex3D
    {
        private GridVector3 _Position;
        private GridVector3 _Normal;

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

        public Vertex3D(GridVector3 p, GridVector3 n) : base()
        {
            _Position = p;
            _Normal = n;
        }

        public Vertex3D(GridVector3 p) : base()
        {
            _Position = p;
            _Normal = GridVector3.Zero;
        }


        public override string ToString()
        {
            return string.Format("I: {0} P: {1} N: {2}", this.Index, Position, Normal);
        }

        public override IVertex ShallowCopy()
        {
            Vertex3D v = new Vertex3D(this.Position, this.Normal);

            return v;
        }

        public int CompareTo(IVertex3D other)
        {
            return this.Index.CompareTo(other.Index);
        }

        public bool Equals(IVertex3D other)
        {
            return this.Index == other.Index;
        }

        int IComparable<IVertex3D>.CompareTo(IVertex3D other)
        {
            return this.Index.CompareTo(other.Index);
        }

        bool IEquatable<IVertex3D>.Equals(IVertex3D other)
        {
            return this.Index == other.Index;
        }
    }


    /// <summary>
    /// A templated vertex that stores additional data of type T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Vertex2D<T> : Vertex2D, IVertex2D<T>
    {
        public T Data { get; set; }

        public Vertex2D(GridVector2 p, T data, IComparer<IEdgeKey> edgeComparer = null) : base(p, edgeComparer)
        {
            Data = data;
        }

        public Vertex2D(GridVector2 p, IComparer<IEdgeKey> edgeComparer = null) : base(p, edgeComparer)
        {
        }

        public override IVertex ShallowCopy()
        {
            Vertex2D<T> newVertex = new Vertex2D<T>(Position, Data, this.EdgeComparer);
            return newVertex;
        }
    }

    /// <summary>
    /// A basic 3D Vertex implementation
    /// </summary>
    public class Vertex2D : VertexBase, IVertex2D
    {
        private GridVector2 _Position;

        public GridVector2 Position
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


        public Vertex2D(GridVector2 p, IComparer<IEdgeKey> edgeComparer = null) : base(edgeComparer)
        {
            _Position = p;
        }

        public Vertex2D(int index, GridVector2 p, IComparer<IEdgeKey> edgeComparer = null) : base(index, edgeComparer)
        {
            _Position = p;
        }

        public override string ToString()
        {
            return string.Format("I: {0} P: {1}", this.Index, Position);
        }

        public int CompareTo(IVertex2D other)
        {
            return this.Index.CompareTo(other.Index);
        }

        public bool Equals(IVertex2D other)
        {
            return this.Index == other.Index;
        }

        public override IVertex ShallowCopy()
        {
            Vertex2D newVertex = new Vertex2D(Position);
            return newVertex;
        }
    }

}
