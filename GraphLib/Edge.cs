using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 

namespace GraphLib
{ 
    public class Edge<NODEKEY> : IComparer<Edge<NODEKEY>>, IComparable<Edge<NODEKEY>>
        where NODEKEY : IComparable<NODEKEY>
    {
        public NODEKEY SourceNodeKey { get; private set; }
        public NODEKEY TargetNodeKey { get; private set; }

        public float Weight = 1;

        public Edge(NODEKEY SourceNode, NODEKEY TargetNode)
        {
            Debug.Assert(SourceNode != null);
            Debug.Assert(TargetNode != null);
            this.SourceNodeKey = SourceNode;
            this.TargetNodeKey = TargetNode;
        }

        public int Compare(Edge<NODEKEY> x, Edge<NODEKEY> y)
        {
            if (x == null && y == null)
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;

            return x.CompareTo(y);
        }

        public int CompareTo(Edge<NODEKEY> other)
        {
            int SourceComparison = this.SourceNodeKey.CompareTo(other.SourceNodeKey);
            int TargetComparison = this.TargetNodeKey.CompareTo(other.TargetNodeKey);

            if (SourceComparison == 0 && TargetComparison == 0)
                return 0;

            if (SourceComparison != 0)
                return SourceComparison;

            return TargetComparison;
        }
    }
}
