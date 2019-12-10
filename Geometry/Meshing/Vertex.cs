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
    /// A basic implementation of the IVertex class to inherit
    /// </summary>
    public abstract class VertexBase : IVertex
    {
        public int Index { get; set; }
        private SortedSet<IEdgeKey> _Edges;

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

        public VertexBase()
        {
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

        public int CompareTo(IVertex other)
        {
            return this.Index.CompareTo(other.Index);
        }

        public bool Equals(IVertex other)
        {
            return this.Index == other.Index;
        }

        public void RemoveEdge(IEdgeKey e)
        {
            Debug.Assert(_Edges.Contains(e));
            _Edges.Remove(e);
            _ImmutableEdges = null;
        }
    }

    /// <summary>
    /// A templated vertex that stores additional data of type T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Vertex3D<T> : Vertex3D
    {
        public T Data;

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
         
        /// <summary>
        /// Duplicate functions are used to create a copy of the vertex without any edge or face data.
        /// </summary>
        /// <param name="oldVertex"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static Vertex3D CreateOffsetCopy(Vertex3D oldVertex, int offset)
        {
            Vertex3D newVertex = new Meshing.Vertex3D(oldVertex.Position, oldVertex.Normal);
            newVertex.Index = oldVertex.Index + offset;
            return newVertex;
        }

        public static IVertex3D CreateOffsetCopy(IVertex3D oldVertex, int offset)
        {
            Vertex3D newVertex = new Meshing.Vertex3D(oldVertex.Position, oldVertex.Normal);
            newVertex.Index = oldVertex.Index + offset;
            return newVertex;
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

        public IVertex ShallowCopy()
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
    }


    /// <summary>
    /// A templated vertex that stores additional data of type T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Vertex2D<T> : Vertex2D
    {
        public T Data;

        public Vertex2D(GridVector2 p, T data) : base(p)
        {
            Data = data;
        }

        public Vertex2D(GridVector2 p) : base(p)
        {
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


        /// <summary>
        /// Duplicate functions are used to create a copy of the vertex without any edge or face data.
        /// </summary>
        /// <param name="oldVertex"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static IVertex2D CreateOffsetCopy(IVertex2D oldVertex, int offset)
        {
            Vertex2D newVertex = new Meshing.Vertex2D(oldVertex.Position);
            newVertex.Index = oldVertex.Index + offset;
            return newVertex;
        }

        public Vertex2D(GridVector2 p) : base()
        {
            _Position = p;
        }
        
        public override string ToString()
        {
            return string.Format("I: {0} P: {1}", this.Index, Position);
        }

        public virtual IVertex2D ShallowCopy()
        {
            Vertex2D v = new Vertex2D(this.Position);

            return v;
        }

        public int CompareTo(IVertex2D other)
        {
            return this.Index.CompareTo(other.Index);
        }

        public bool Equals(IVertex2D other)
        {
            return this.Index == other.Index;
        }
    }

}
