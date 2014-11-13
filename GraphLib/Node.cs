using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GraphLib
{
    public class Node<KEY, EDGETYPE> : IComparer<Node<KEY, EDGETYPE>>, IComparable<Node<KEY, EDGETYPE>>
        where KEY : IComparable<KEY>, IEquatable<KEY>
        where EDGETYPE : Edge<KEY>
    {
        public KEY Key; 

        /// <summary>
        /// Keys are the ID of the other node in the edge, or our iD if it is a circular reference
        /// </summary>
        public SortedDictionary<KEY, List<EDGETYPE>> Edges = new SortedDictionary<KEY, List<EDGETYPE>>();

        public Node(KEY k)
        {
            this.Key = k;
        }

        public void AddEdge(EDGETYPE Link)
        { 
            
            KEY PartnerKey = Link.SourceNodeKey;
            if (Link.SourceNodeKey.CompareTo(Link.TargetNodeKey) == 0)
            {
                //Circular reference, just proceed
            }
            else if (Link.SourceNodeKey.CompareTo(this.Key) == 0)
            {
                PartnerKey = Link.TargetNodeKey;
            }

            List<EDGETYPE> edgeList = null; 
            if( Edges.ContainsKey(PartnerKey))
            {
                edgeList = Edges[PartnerKey]; 
            }
            else
            {
                edgeList = new List<EDGETYPE>();
                Edges[PartnerKey] = edgeList; 
            }
                
            edgeList.Add(Link); 
            
        }


        public int Compare(Node<KEY, EDGETYPE> x, Node<KEY, EDGETYPE> y)
        {
            return this.CompareTo(y); 
        }

        public int CompareTo(Node<KEY, EDGETYPE> other)
        {
            return this.Key.CompareTo(other.Key);
        }
    }
}
