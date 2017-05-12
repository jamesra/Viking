using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace GraphLib
{
    [Serializable]
    public abstract class Edge<NODEKEY> : IComparer<Edge<NODEKEY>>, IComparable<Edge<NODEKEY>>, IEquatable<Edge<NODEKEY>>, ISerializable
        where NODEKEY : IComparable<NODEKEY>, IEquatable<NODEKEY>
    {
        public readonly NODEKEY SourceNodeKey;
        public readonly NODEKEY TargetNodeKey;

        public virtual bool Directional { get; set; }

        public virtual float Weight  { get { return 1.0f; } }

        public bool IsLoop {  get { return SourceNodeKey.Equals(TargetNodeKey); } }

        public Edge(NODEKEY SourceNode, NODEKEY TargetNode, bool Directional)
        {
            Debug.Assert(SourceNode != null);
            Debug.Assert(TargetNode != null);
            this.SourceNodeKey = SourceNode;
            this.TargetNodeKey = TargetNode;
            this.Directional = Directional;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("SourceNodeKey", SourceNodeKey, typeof(NODEKEY));
            info.AddValue("TargetNodeKey", TargetNodeKey, typeof(NODEKEY));
            info.AddValue("Directional", Directional, typeof(bool));
            info.AddValue("Weight", Weight, typeof(float));
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

        protected NODEKEY[] OrderedNodeKeys()
        {
            NODEKEY lowestKey = this.SourceNodeKey.CompareTo(this.TargetNodeKey) < 0 ? this.SourceNodeKey : this.TargetNodeKey;
            NODEKEY highestKey = this.SourceNodeKey.CompareTo(this.TargetNodeKey) < 0 ? this.TargetNodeKey : this.SourceNodeKey;
            return new NODEKEY[] { lowestKey, highestKey };
        } 
        protected int CompareToDirectional(Edge<NODEKEY> other)
        {
            int SourceComparison = this.SourceNodeKey.CompareTo(other.SourceNodeKey);
            int TargetComparison = this.TargetNodeKey.CompareTo(other.TargetNodeKey);

            if (SourceComparison == 0 && TargetComparison == 0)
                return 0;

            if (SourceComparison != 0)
                return SourceComparison;

            return TargetComparison;
        } 
        protected int CompareToBidirectional(Edge<NODEKEY> other)
        {
            NODEKEY[] thisKeys = this.OrderedNodeKeys();
            NODEKEY[] otherKeys = other.OrderedNodeKeys();

            //Find if any key comparisons are unequal
            int[] comparisons = thisKeys.Select((key, i) => key.CompareTo(otherKeys[i])).Where(c => c != 0).ToArray();

            if (comparisons.Length == 0)
                return 0;

            return comparisons.First();
        }

        public virtual int CompareTo(Edge<NODEKEY> other)
        {
            int DirectionalCompare = this.Directional.CompareTo(other.Directional);
            if (DirectionalCompare != 0)
                return DirectionalCompare;

            if (Directional)
            {
                return CompareToDirectional(other);
            }
            else
            {
                return CompareToBidirectional(other);
            }
        }

        public bool Equals(Edge<NODEKEY> other)
        {
            if (object.ReferenceEquals(this, other))
                return true;

            if ((object)other == null)
                return false;

            if (this.Directional != other.Directional)
                return false; 

            if(Directional)
                return this.SourceNodeKey.Equals(other.SourceNodeKey) && this.TargetNodeKey.Equals(other.TargetNodeKey);
            else
            {
                return (this.SourceNodeKey.Equals(other.SourceNodeKey) && this.TargetNodeKey.Equals(other.TargetNodeKey)) ||
                       (this.SourceNodeKey.Equals(other.TargetNodeKey) && this.TargetNodeKey.Equals(other.SourceNodeKey));
            }
        }

        public override int GetHashCode()
        {
            return (SourceNodeKey.GetHashCode() / 2) + (TargetNodeKey.GetHashCode() / 2);
        }

        public override bool Equals(object obj)
        {
            if (obj as Edge<NODEKEY> != null)
                return this.Equals((Edge<NODEKEY>)obj);

            return base.Equals(obj);
        }
        
        public static bool operator ==(Edge<NODEKEY> A, Edge<NODEKEY> B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(Edge<NODEKEY> A, Edge<NODEKEY> B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if ((object)A != null)
                return !A.Equals(B);

            return true;
        }

        public override string ToString()
        {
            if(this.Directional)
                return string.Format("{0}  -> {1}", this.SourceNodeKey, this.TargetNodeKey);
            else
                return string.Format("{0} <-> {1}", this.SourceNodeKey, this.TargetNodeKey);
        }
    }
}
