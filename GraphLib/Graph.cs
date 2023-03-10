using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace GraphLib
{
    [Serializable]
    public partial class Graph<KEY, NODETYPE, EDGETYPE> : ISerializable
        where KEY : IComparable<KEY>, IEquatable<KEY>
        where NODETYPE : Node<KEY, EDGETYPE>
        where EDGETYPE : Edge<KEY>
    {
        // Contains all _Edges
        public readonly SortedList<EDGETYPE, EDGETYPE> _Edges;
        public IReadOnlyDictionary<EDGETYPE, EDGETYPE> Edges => _Edges;

        protected readonly Dictionary<KEY, NODETYPE> _Nodes;

        public IReadOnlyDictionary<KEY, NODETYPE> Nodes => _Nodes;

        public bool ContainsKey(KEY key) => _Nodes.ContainsKey(key);

        public Graph()
        {
            _Edges = new SortedList<EDGETYPE, EDGETYPE>();
            _Nodes = new Dictionary<KEY, NODETYPE>();
        }

        public Graph(IEnumerable<NODETYPE> _Nodes, IEnumerable<EDGETYPE> _Edges) : this()
        {
            foreach (var n in _Nodes)
            {
                this.AddNode(n);
            }

            foreach (var e in _Edges)
            {
                this.AddEdge(e);
            }
        }

        public Graph(SerializationInfo info, StreamingContext context)
        {
            _Edges = (SortedList<EDGETYPE, EDGETYPE>)info.GetValue("_Edges", typeof(SortedList<EDGETYPE, EDGETYPE>));
            _Nodes = (Dictionary<KEY, NODETYPE>)info.GetValue("_Nodes", typeof(Dictionary<KEY, NODETYPE>));

            foreach (EDGETYPE e in _Edges.Values)
            {
                NODETYPE source = _Nodes[e.SourceNodeKey];
                NODETYPE target = _Nodes[e.TargetNodeKey];

                source.AddEdge(e);
                target.AddEdge(e);
            }
        }

        public NODETYPE this[KEY key] => _Nodes[key];

        /// <summary>
        /// Returns the set of _Edges from Source to Target, or an empty set
        /// if the edge does not exist
        /// </summary>
        /// <param name="Source"></param>
        /// <param name="Target"></param>
        /// <returns></returns>
        public SortedSet<EDGETYPE> this[KEY Source, KEY Target]
        {
            get
            {
                if (this._Nodes.TryGetValue(Source, out NODETYPE node))
                {
                    if (node.Edges.TryGetValue(Target, out var Result))
                    {
                        return Result;
                    }
                }

                return new SortedSet<EDGETYPE>();
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("_Edges", _Edges, typeof(SortedList<EDGETYPE, EDGETYPE>));
            info.AddValue("_Nodes", _Nodes, typeof(Dictionary<KEY, NODETYPE>));
        }

        public virtual void AddNode(NODETYPE node)
        {
            this._Nodes.Add(node.Key, node);
        }

        /// <summary>
        /// Remove the node, remove all _Edges to the node
        /// </summary>
        /// <param name="key"></param>
        public virtual void RemoveNode(KEY key)
        {
            NODETYPE node_to_remove = _Nodes[key];
            SortedSet<KEY> other_nodes = new SortedSet<KEY>(node_to_remove.Edges.Keys);

            //Remove edge from other _Nodes to our node
            foreach (KEY other_id in other_nodes)
            {
                RemoveAllEdges(key, other_id);
            }

            this._Nodes.Remove(key);
        }

        /// <summary>
        /// Remove all _Edges between these two _Nodes
        /// </summary>
        /// <param name="key"></param>
        /// <param name="other_id"></param>
        private void RemoveAllEdges(KEY key, KEY other_id)
        {
            NODETYPE other_node = _Nodes[other_id];

            ICollection<EDGETYPE> edges_to_remove = other_node.Edges[key].ToList();

            foreach (EDGETYPE edge_to_removed in edges_to_remove)
            {
                this.RemoveEdge(edge_to_removed);
            }
        }

        public void AddEdge(EDGETYPE edge)
        {
            Debug.Assert(_Nodes.ContainsKey(edge.SourceNodeKey));
            Debug.Assert(_Nodes.ContainsKey(edge.TargetNodeKey));
            Debug.Assert(!_Edges.ContainsKey(edge));

            this._Nodes[edge.SourceNodeKey].AddEdge(edge);
            this._Nodes[edge.TargetNodeKey].AddEdge(edge);

            this._Edges.Add(edge, edge);
        }

        public void RemoveEdge(EDGETYPE edge)
        {
            Debug.Assert(_Nodes.ContainsKey(edge.SourceNodeKey));
            Debug.Assert(_Nodes.ContainsKey(edge.TargetNodeKey));
            if (!_Edges.ContainsKey(edge))
            {
                throw new ArgumentException(string.Format("Edge does not exist in graph {0}", edge));
            }

            this._Nodes[edge.SourceNodeKey].RemoveEdge(edge);
            this._Nodes[edge.TargetNodeKey].RemoveEdge(edge);

            this._Edges.Remove(edge);
        }


    }
}