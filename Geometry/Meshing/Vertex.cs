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
        /// <summary>
        /// Index of the vertex in a mesh.  Can only be set once.  If a different index is desired use CreateShallowCopy(int).
        /// </summary>
        public int Index => _Index ?? throw new InvalidOperationException("No index set for vertex yet");

        private int? _Index = null;

        public bool HasIndex => _Index.HasValue;

        public void SetIndex(int index)
        {
            if (_Index.HasValue && index != this.Index)
                throw new InvalidOperationException("Index already set for vertex");
            
            _Index = index;
        }

        public IComparer<IEdgeKey> EdgeComparer
        {
            get => _Edges.Comparer;
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
        public ImmutableSortedSet<IEdgeKey> Edges => _ImmutableEdges ?? (_ImmutableEdges = _Edges.ToImmutableSortedSet(_Edges.Comparer));

        protected VertexBase()
        {
            _Edges = new SortedSet<IEdgeKey>();
            _ImmutableEdges = null;
        }

        protected VertexBase(int index) : this()
        {
            this._Index = index;
        }

        protected VertexBase(IComparer<IEdgeKey> edgeComparer = null)
        {
            _Edges = new SortedSet<IEdgeKey>(edgeComparer);
            _ImmutableEdges = null;
        }

        protected VertexBase(int index, IComparer<IEdgeKey> edgeComparer = null) : this(edgeComparer)
        {
            this._Index = index;
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
            if (!_Index.HasValue) throw new InvalidOperationException("Index must be set before Equals is called.");

            if (other is null)
                return false;

            return this.Index == other.Index;
        }

        public bool Equals(VertexBase other)
        {
            if (!_Index.HasValue) throw new InvalidOperationException("Index must be set before Equals is called.");

            if (other is null)
                return false;

            return this.Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            if (!_Index.HasValue) throw new InvalidOperationException("Index must be set before Equals is called.");

            switch (obj)
            {
                case VertexBase other:
                    return this.Index == other.Index;
                case IVertex other2:
                    return this.Index == other2.Index;
                default:
                    return base.Equals(obj);
            }
        }

        public override int GetHashCode()
        {
            return _Index.HasValue ? _Index.Value : throw new InvalidOperationException("Index must be set before GetHashCode is called.");
        }

        public virtual void RemoveEdge(IEdgeKey e)
        {
            Debug.Assert(_Edges.Contains(e));
            _Edges.Remove(e);
            _ImmutableEdges = null;
        }

        public abstract IVertex ShallowCopy();

        public abstract IVertex ShallowCopy(int index);
    }

    /// <summary>
    /// A templated vertex that stores additional data of type T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Vertex3D<T> : Vertex3D, IVertex3D<T>
    {
        public T Data { get; set; }

        public Vertex3D(int index, GridVector3 p, GridVector3 n, T data) : base(index, p, n)
        {
            Data = data;
        }

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

        public override IVertex ShallowCopy(int index)
        {
            Vertex3D<T> newVertex = new Vertex3D<T>(index, Position, Normal, Data);
            return newVertex;
        }
    }

    /// <summary>
    /// A basic 3D Vertex implementation
    /// </summary>
    public class Vertex3D : VertexBase, IVertex3D
    {
        private GridVector3 _Position;

        public GridVector3 Position
        {
            get => _Position;
            set => _Position = value;
        }

        public GridVector3 Normal { get; set; }

        public Vertex3D(GridVector3 p, GridVector3 n) : base()
        {
            _Position = p;
            Normal = n;
        }

        public Vertex3D(GridVector3 p) : base()
        {
            _Position = p;
            Normal = GridVector3.Zero;
        }

        public Vertex3D(int index, GridVector3 p, GridVector3 n) : base(index)
        {
            _Position = p;
            Normal = n;
        } 

        public Vertex3D(int index, GridVector3 p) : base(index)
        {
            _Position = p;
            Normal = GridVector3.Zero;
        }


        public override string ToString()
        {
            return $"I: {this.Index} P: {Position} N: {Normal}";
        }

        public override IVertex ShallowCopy()
        {
            Vertex3D v = new Vertex3D(this.Position, this.Normal);

            return v;
        }

        public override IVertex ShallowCopy(int index)
        {
            return new Vertex3D(index, this.Position, this.Normal);
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
            if (other is null)
                return 1;

            return this.Index.CompareTo(other.Index);
        }

        bool IEquatable<IVertex3D>.Equals(IVertex3D other)
        {
            if (other is null)
                return false;

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

        public Vertex2D(int index, GridVector2 p, T data, IComparer<IEdgeKey> edgeComparer = null) : base(index, p, edgeComparer)
        {
            Data = data;
        }

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

        public override IVertex ShallowCopy(int index)
        {
            Vertex2D<T> newVertex = new Vertex2D<T>(index, Position, Data, this.EdgeComparer);
            return newVertex;
        }

        public override string ToString()
        {
            return Data == null ? $"I: {this.Index} P: {Position}" : $"I: {this.Index} P: {Position} Data: {Data?.ToString()}";
        }
    }

    /// <summary>
    /// A basic 3D Vertex implementation
    /// </summary>
    public class Vertex2D : VertexBase, IVertex2D
    {
        public GridVector2 Position { get; set; }


        public Vertex2D(GridVector2 p, IComparer<IEdgeKey> edgeComparer = null) : base(edgeComparer)
        {
            Position = p;
        }

        public Vertex2D(int index, GridVector2 p, IComparer<IEdgeKey> edgeComparer = null) : base(index, edgeComparer)
        {
            Position = p;
        }

        public override string ToString()
        {
            return $"I: {this.Index} P: {Position}";
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

        public override IVertex ShallowCopy(int index)
        {
            Vertex2D newVertex = new Vertex2D(index, Position);
            return newVertex;
        }
    }

}
