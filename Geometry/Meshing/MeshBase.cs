using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Geometry.Meshing
{
    
    public interface IMesh<VERTEX>
        where VERTEX : IVertex
    {
        List<VERTEX> Verticies { get; }
        SortedList<IEdgeKey, IEdge> Edges { get; }
        SortedSet<IFace> Faces { get; }

        VERTEX this[long index] { get; }
        VERTEX this[int index] { get; }

        IEnumerable<VERTEX> this[IEnumerable<int> vertIndicies] { get; }
        IEnumerable<VERTEX> this[IEnumerable<long> vertIndicies] { get; }

        IEdge this[IEdgeKey key] { get; }

        bool Contains(IEdgeKey key);
        bool Contains(IFace key);

        bool Contains(int A, int B);

        /// <summary>
        /// Add vertex to the mesh
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Index of vertex</returns>
        int AddVertex(VERTEX v);

        int AddVerticies(ICollection<VERTEX> verts);

        void AddEdge(int A, int B);

        void AddEdge(IEdgeKey e);

        void AddEdge(IEdge e);

        void RemoveEdge(IEdgeKey e);

        void AddFace(IFace face);

        void AddFace(int A, int B, int C);

        void AddFaces(ICollection<IFace> faces);

        void RemoveFace(IFace f);
    }

    
    /// <summary>
    /// A class that implements the basic mesh operations
    /// </summary>
    /// <typeparam name="VERTEX"></typeparam>
    public abstract class MeshBase<VERTEX> : IMesh<VERTEX>
        where VERTEX : IVertex
    {
        protected readonly List<VERTEX> _Verticies = new List<VERTEX>();
        protected readonly SortedList<IEdgeKey, IEdge> _Edges = new SortedList<IEdgeKey, IEdge>();
        protected readonly SortedSet<IFace> _Faces = new SortedSet<IFace>();

        public List<VERTEX> Verticies { get { return _Verticies; } }
        public SortedList<IEdgeKey, IEdge> Edges { get { return _Edges; } }
        public SortedSet<IFace> Faces { get { return _Faces; } }

        /* Functions for mesh users to override how mesh objects are created*/
        public Func<VERTEX, int, VERTEX> CreateOffsetVertex { get; set; }
        public Func<IEdge, int, int, IEdge> CreateOffsetEdge { get; set; }
        public Func<IFace, IEnumerable<int>, IFace> CreateOffsetFace { get; set; }
        public Func<int, VERTEX> CreateVertex { get; set; }
        public Func<int, int, IEdge> CreateEdge { get; set; }
        public Func<IEnumerable<int>, IFace> CreateFace { get; set; }

        public virtual VERTEX this[int key]
        {
            get
            {
                return _Verticies[key];
            }
            set
            {
                _Verticies[key] = value;
            }
        }

        public virtual VERTEX this[long key]
        {
            get
            {
                return _Verticies[(int)key];
            }
            set
            {
                _Verticies[(int)key] = value;
            }
        }
         
        /// <summary>
        /// Returns all of the verticies that match the indicies
        /// </summary>
        /// <param name="vertIndicies"></param>
        /// <returns></returns>
        public IEnumerable<VERTEX> this[IEnumerable<int> vertIndicies]
        {
            get
            {
                return vertIndicies.Select(i => this._Verticies[(int)i]);
            }
        }

        /// <summary>
        /// Returns all of the verticies that match the indicies
        /// </summary>
        /// <param name="vertIndicies"></param>
        /// <returns></returns>
        public IEnumerable<VERTEX> this[IEnumerable<long> vertIndicies]
        {
            get
            {
                return vertIndicies.Select(i => this._Verticies[(int)i]);
            }
        }

        public virtual IEdge this[IEdgeKey key]
        {
            get { return this._Edges[key]; }
        }

        public virtual bool Contains(IEdgeKey key)
        {
            return Edges.ContainsKey(key);
        }

        public virtual bool Contains(IFace face)
        {
            return Faces.Contains(face);
        }

        public virtual bool Contains(int A, int B)
        {
            return Edges.ContainsKey(new EdgeKey(A, B));
        }

        public virtual int AddVertex(VERTEX v)
        {
            v.Index = _Verticies.Count;
            _Verticies.Add(v);

            UpdateBoundingBox(v);
            return _Verticies.Count - 1;
        }

        /// <summary>
        /// Add a collection of verticies to the mesh
        /// </summary>
        /// <param name="v"></param>
        /// <returns>The index the first element was inserted at</returns>
        public virtual int AddVerticies(ICollection<VERTEX> verts)
        {

            int iStart = _Verticies.Count;
            int Offset = 0;
            foreach (IVertex v in verts)
            {
                v.Index = iStart + Offset;
                Offset += 1;
            }

            _Verticies.AddRange(verts);
            UpdateBoundingBox(verts);
            return iStart;
        }


        protected abstract void UpdateBoundingBox(VERTEX point);
        protected abstract void UpdateBoundingBox(ICollection<VERTEX> points);

        public void AddEdge(int A, int B)
        {
            EdgeKey e = new EdgeKey(A, B);
            AddEdge(e);
        }

        public void AddEdge(IEdgeKey e)
        {
            if (e.A == e.B)
                throw new ArgumentException("Edges cannot have the same start and end point");

            if (CreateOffsetEdge == null)
                throw new InvalidOperationException("DuplicateEdge function not specified for DynamicRenderMesh");

            if (this.Contains(e))
                return;

            if (e.A >= _Verticies.Count || e.A < 0)
                throw new ArgumentException(string.Format("Edge vertex A references non-existent vertex {0}", e));

            if (e.B >= _Verticies.Count || e.B < 0)
                throw new ArgumentException(string.Format("Edge vertex B references non-existent vertex {0}", e));

            IEdge newEdge = CreateOffsetEdge(null, e.A, e.B);
            Edges.Add(e, newEdge);

            _Verticies[(int)e.A].AddEdge(e);
            _Verticies[(int)e.B].AddEdge(e);
        }


        public void AddEdge(IEdge e)
        {
            if (e.A == e.B)
                throw new ArgumentException("Edges cannot have the same start and end point");

            if (this.Contains(e.Key))
                return;

            if (e.A >= _Verticies.Count || e.A < 0)
                throw new ArgumentException(string.Format("Edge vertex A references non-existent vertex {0}", e));

            if (e.B >= _Verticies.Count || e.B < 0)
                throw new ArgumentException(string.Format("Edge vertex B references non-existent vertex {0}", e));

            Edges.Add(e.Key, e);

            _Verticies[(int)e.A].AddEdge(e.Key);
            _Verticies[(int)e.B].AddEdge(e.Key);
        }

        public void RemoveEdge(IEdgeKey e)
        {
            if (_Edges.ContainsKey(e))
            {
                IEdge removedEdge = _Edges[e];

                foreach (IFace f in removedEdge.Faces)
                {
                    this.RemoveFace(f);
                }

                _Edges.Remove(e);

                this[removedEdge.A].RemoveEdge(e);
                this[removedEdge.B].RemoveEdge(e);
            }
        }
         
        /// <summary>
        /// Add a face. Creates edges if they aren't in the face
        /// </summary>
        /// <param name="face"></param>
        public void AddFace(IFace face)
        {
            //Debug.Assert(Faces.Contains(face) == false);

            foreach (IEdgeKey e in face.Edges)
            {
                AddEdge(e);
                Edges[e].AddFace(face);
            }

            Faces.Add(face);
        }

        public void AddFace(int A, int B, int C)
        {
            IFace face = CreateFace(new int[] { A, B, C });
            Debug.Assert(Faces.Contains(face) == false);

            AddFace(face);
        }

        public void AddFaces(ICollection<IFace> faces)
        {
            foreach (IFace f in faces)
            {
                AddFace(f);
            }
        }

        public void RemoveFace(IFace f)
        {
            if (Faces.Contains(f))
            {
                Faces.Remove(f);
            }

            foreach (IEdgeKey e in f.Edges)
            {
                IEdge existing = Edges[e];
                existing.RemoveFace(f);
            }
        }

        
    }
}
