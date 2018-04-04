using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace GraphLib
{
    [Serializable]
    public partial class Graph<KEY,NODETYPE, EDGETYPE> : ISerializable
        where KEY : IComparable<KEY>, IEquatable<KEY>
        where NODETYPE : Node<KEY, EDGETYPE>
        where EDGETYPE : Edge<KEY>
    {
        // Contains all edges
        public SortedList<EDGETYPE, EDGETYPE> Edges { get;  }

        public Dictionary<KEY,NODETYPE> Nodes { get; }
        
        public Graph()
        {
            Edges = new SortedList<EDGETYPE, EDGETYPE>();
            Nodes = new Dictionary<KEY, NODETYPE>();
        }

        public Graph(SerializationInfo info, StreamingContext context)
        {
            Edges = (SortedList<EDGETYPE, EDGETYPE>)info.GetValue("Edges", typeof(SortedList<EDGETYPE, EDGETYPE>));
            Nodes = (Dictionary<KEY, NODETYPE>)info.GetValue("Nodes", typeof(Dictionary<KEY, NODETYPE>));

            foreach(EDGETYPE e in Edges.Values)
            {
                NODETYPE source = Nodes[e.SourceNodeKey];
                NODETYPE target = Nodes[e.TargetNodeKey];

                source.AddEdge(e);
                target.AddEdge(e);
            }
        }

        public NODETYPE this[KEY key]
        {
            get { return this.Nodes[key]; }
        }
        
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Edges", Edges, typeof(SortedList<EDGETYPE, EDGETYPE>));
            info.AddValue("Nodes", Nodes, typeof(Dictionary<KEY, NODETYPE>));
        }

        public virtual void AddNode(NODETYPE node)
        {
            this.Nodes.Add(node.Key, node); 
        }

        /// <summary>
        /// Remove the node, remove all edges to the node
        /// </summary>
        /// <param name="key"></param>
        public virtual void RemoveNode(KEY key)
        {
            NODETYPE node_to_remove = Nodes[key];
            SortedSet<KEY> other_nodes = new SortedSet<KEY>(node_to_remove.Edges.Keys);

            //Remove edge from other nodes to our node
            foreach (KEY other_id in other_nodes)
            {
                RemoveAllEdges(key, other_id);
            }

            this.Nodes.Remove(key);
        }

        /// <summary>
        /// Remove all edges between these two nodes
        /// </summary>
        /// <param name="key"></param>
        /// <param name="other_id"></param>
        private void RemoveAllEdges(KEY key, KEY other_id)
        {
            NODETYPE other_node = Nodes[other_id];

            ICollection<EDGETYPE> edges_to_remove = other_node.Edges[key].ToList();

            foreach (EDGETYPE edge_to_removed in edges_to_remove)
            {
                this.RemoveEdge(edge_to_removed);
            }
        }

        public void AddEdge(EDGETYPE edge)
        {
            Debug.Assert(Nodes.ContainsKey(edge.SourceNodeKey));
            Debug.Assert(Nodes.ContainsKey(edge.TargetNodeKey));
            Debug.Assert(!Edges.ContainsKey(edge));

            this.Nodes[edge.SourceNodeKey].AddEdge(edge);
            this.Nodes[edge.TargetNodeKey].AddEdge(edge);

            this.Edges.Add(edge, edge);
        }

        public void RemoveEdge(EDGETYPE edge)
        {
            Debug.Assert(Nodes.ContainsKey(edge.SourceNodeKey));
            Debug.Assert(Nodes.ContainsKey(edge.TargetNodeKey));
            if(!Edges.ContainsKey(edge))
            {
                throw new ArgumentException(string.Format("Edge does not exist in graph {0}", edge));
            } 

            this.Nodes[edge.SourceNodeKey].RemoveEdge(edge);
            this.Nodes[edge.TargetNodeKey].RemoveEdge(edge);

            this.Edges.Remove(edge);
        }

        
    }
}