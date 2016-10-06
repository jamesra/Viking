using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Diagnostics;

namespace GraphLib
{
    public partial class Graph<KEY,NODETYPE, EDGETYPE> 
        where KEY : IComparable<KEY>, IEquatable<KEY>
        where NODETYPE : Node<KEY, EDGETYPE>
        where EDGETYPE : Edge<KEY>
    {
        // Contains all edges
        public SortedList<EDGETYPE, EDGETYPE> Edges = new SortedList<EDGETYPE, EDGETYPE>();

        public Dictionary<KEY,NODETYPE> Nodes = new Dictionary<KEY,NODETYPE>(); 
        
        public Graph()
        {
            
        }

        public void AddNode(NODETYPE node)
        {
            this.Nodes.Add(node.Key, node); 
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
    }
}