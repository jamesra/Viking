using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 

namespace GraphLib
{ 
    public class Edge<NODEKEY> : IComparer<Edge<NODEKEY>>, IComparable<Edge<NODEKEY>>, IEquatable<Edge<NODEKEY>>
        where NODEKEY : IComparable<NODEKEY>, IEquatable<NODEKEY>
    {
        public NODEKEY SourceNodeKey { get; private set; }
        public NODEKEY TargetNodeKey { get; private set; }

        public virtual float Weight  { get { return 1.0f; } }

        public bool IsLoop {  get { return SourceNodeKey.Equals(TargetNodeKey); } }

        public Edge(NODEKEY SourceNode, NODEKEY TargetNode)
        {
            Debug.Assert(SourceNode != null);
            Debug.Assert(TargetNode != null);
            this.SourceNodeKey = SourceNode;
            this.TargetNodeKey = TargetNode;
        }

        public virtual int Compare(Edge<NODEKEY> x, Edge<NODEKEY> y)
        {
            if (object.ReferenceEquals(x, y))
                return 0;

            if ((object)x == null && (object)y == null)
                return 0;
            if ((object)x == null)
                return -1;
            if ((object)y == null)
                return 1;

            return x.CompareTo(y);
        }

        public virtual int CompareTo(Edge<NODEKEY> other)
        {
            int SourceComparison = this.SourceNodeKey.CompareTo(other.SourceNodeKey);
            int TargetComparison = this.TargetNodeKey.CompareTo(other.TargetNodeKey);

            if (SourceComparison == 0 && TargetComparison == 0)
                return 0;

            if (SourceComparison != 0)
                return SourceComparison;

            return TargetComparison;
        }

        public bool Equals(Edge<NODEKEY> other)
        {
            if (object.ReferenceEquals(this, other))
                return true;

            if ((object)other == null)
                return false;

            return this.SourceNodeKey.Equals(other.SourceNodeKey) && this.TargetNodeKey.Equals(other.TargetNodeKey);
        }
    }
}
