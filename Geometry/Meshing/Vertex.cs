using System;
using System.Diagnostics;
using System.Collections.Generic;
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
    }

    public class Vertex : IVertex 
    {
        public GridVector3 _Position;
        public GridVector3 _Normal;
        public SortedSet<Edge> _Edges;

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

        public SortedSet<Edge> Edges
        {
            get
            {
                return _Edges;
            }
        }

        public Vertex(GridVector3 p, GridVector3 n)
        {
            _Position = p;
            _Normal = n;
            _Edges = new SortedSet<Edge>();
        }
        
        public void AddEdge(Edge e)
        {
            Debug.Assert(_Edges.Contains(e) == false);
            _Edges.Add(e);
        }

        public void RemoveEdge(Edge e)
        {
            Debug.Assert(_Edges.Contains(e));
            _Edges.Remove(e);
        }
    }

}
