using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

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
            get => _ifaceEdgeComparer;
            protected set
            {
                if (value == _ifaceEdgeComparer)
                    return;

                _ifaceEdgeComparer = value;
                IComparer<EdgeKey> comparer = value as IComparer<EdgeKey>
                    ?? (value is null ? Comparer<EdgeKey>.Default : new EdgeKeyInterfaceComparer(value));
                _Edges = new SortedSet<EdgeKey>(_Edges, comparer);
                _ImmutableEdges = null;
            }
        }

        IComparer<IEdgeKey> _ifaceEdgeComparer;

        protected SortedSet<EdgeKey> _Edges;

        private ImmutableSortedSet<IEdgeKey> _ImmutableEdges;
        public ImmutableSortedSet<IEdgeKey> Edges => _ImmutableEdges ??= ImmutableSortedSet.CreateRange(
            _ifaceEdgeComparer ?? Comparer<IEdgeKey>.Default,
            _Edges.Select(static e => (IEdgeKey)e));

        protected VertexBase()
        {
            _Edges = [];
            _ImmutableEdges = null;
        }

        protected VertexBase(int index) : this()
        {
            this._Index = index;
        }

        protected VertexBase(IComparer<IEdgeKey> edgeComparer = null)
        {
            _Edges = [];
            _ImmutableEdges = null;
            if (edgeComparer is not null)
                EdgeComparer = edgeComparer;
        }

        protected VertexBase(int index, IComparer<IEdgeKey> edgeComparer = null) : this(edgeComparer)
        {
            this._Index = index;
        }

        public virtual bool AddEdge(IEdgeKey e)
        {
            EdgeKey key = e is EdgeKey ek ? ek : new EdgeKey(e.A, e.B);
            return AddEdge(key);
        }

        public bool AddEdge(EdgeKey e)
        {
            if (!_Edges.Contains(e))
            {
                _Edges.Add(e);
                _ImmutableEdges = null;

                return true;
            }

            return false;
        }

        public int CompareTo(IVertex other) => this.Index.CompareTo(other.Index);

        public int CompareTo(VertexBase other) => this.Index.CompareTo(other.Index);

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

            return obj switch
            {
                VertexBase other => this.Index == other.Index,
                IVertex other2 => this.Index == other2.Index,
                _ => base.Equals(obj),
            };
        }

        public override int GetHashCode() => _Index ?? throw new InvalidOperationException("Index must be set before GetHashCode is called.");

        public virtual void RemoveEdge(IEdgeKey e)
        {
            EdgeKey key = e is EdgeKey ek ? ek : new EdgeKey(e.A, e.B);
            RemoveEdge(key);
        }

        public void RemoveEdge(EdgeKey e)
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

        public override IVertex ShallowCopy() => new Vertex3D<T>(Position, Normal, Data);

        public override IVertex ShallowCopy(int index) => new Vertex3D<T>(index, Position, Normal, Data);
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


        public override string ToString() => $"I: {this.Index} P: {Position} N: {Normal}";

        public override IVertex ShallowCopy() => new Vertex3D(this.Position, this.Normal);

        public override IVertex ShallowCopy(int index) => new Vertex3D(index, this.Position, this.Normal);

        public int CompareTo(IVertex3D other) => this.Index.CompareTo(other.Index);

        public bool Equals(IVertex3D other) => this.Index == other.Index;

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

        public override IVertex ShallowCopy() => new Vertex2D<T>(Position, Data, this.EdgeComparer);

        public override IVertex ShallowCopy(int index) => new Vertex2D<T>(index, Position, Data, this.EdgeComparer);

        public override string ToString() => Data is null ? $"I: {this.Index} P: {Position}" : $"I: {this.Index} P: {Position} Data: {Data?.ToString()}";
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

        public override string ToString() => $"I: {this.Index} P: {Position}";

        public int CompareTo(IVertex2D other) => this.Index.CompareTo(other.Index);

        public bool Equals(IVertex2D other) => this.Index == other.Index;

        public override IVertex ShallowCopy() => new Vertex2D(Position);

        public override IVertex ShallowCopy(int index) => new Vertex2D(index, Position);
    }

    sealed class EdgeKeyInterfaceComparer(IComparer<IEdgeKey> inner) : IComparer<EdgeKey>
    {
        public int Compare(EdgeKey x, EdgeKey y) => inner.Compare(x, y);
    }

}
