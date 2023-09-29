using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GraphLib
{
    [Serializable]
    public class Node<KEY, EDGETYPE> : IComparer<Node<KEY, EDGETYPE>>, IComparable<Node<KEY, EDGETYPE>>, IEquatable<Node<KEY, EDGETYPE>>, ISerializable
        where KEY : IComparable<KEY>, IEquatable<KEY>
        where EDGETYPE : Edge<KEY>
    {
        public readonly KEY Key;

        /// <summary>
        /// Keys are the ID of the other node in the edge, or our iD if it is a circular reference
        /// </summary>
        private readonly SortedDictionary<KEY, SortedSet<EDGETYPE>> _Edges = new SortedDictionary<KEY, SortedSet<EDGETYPE>>();

        public IReadOnlyDictionary<KEY, SortedSet<EDGETYPE>> Edges => _Edges;

        /// <summary>
        /// A collection of additional attributes that have been added to the node
        /// </summary>
        public readonly Dictionary<string, object> Attributes = new Dictionary<string, object>();

        public Node(KEY k)
        {
            this.Key = k;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Key", Key, typeof(KEY));
        }

        internal void AddEdge(EDGETYPE Link)
        {
            KEY PartnerKey = Link.SourceNodeKey;
            if (Link.IsLoop)
            {
                //Circular reference, just proceed
            }
            else if (Link.SourceNodeKey.CompareTo(this.Key) == 0)
            {
                PartnerKey = Link.TargetNodeKey;
            }

            SortedSet<EDGETYPE> edgeList = null;
            if (_Edges.ContainsKey(PartnerKey))
            {
                edgeList = _Edges[PartnerKey];
            }
            else
            {
                edgeList = new SortedSet<EDGETYPE>();
                _Edges[PartnerKey] = edgeList;
            }

            edgeList.Add(Link);
        }

        internal void RemoveEdge(KEY other)
        {
            if (_Edges.ContainsKey(other))
            {
                _Edges.Remove(other);
            }
        }

        internal void RemoveEdge(EDGETYPE Link)
        {
            KEY PartnerKey = Link.SourceNodeKey;
            if (Link.IsLoop)
            {
                //Circular reference, just proceed
            }
            else if (Link.SourceNodeKey.CompareTo(this.Key) == 0)
            {
                PartnerKey = Link.TargetNodeKey;
            }

            SortedSet<EDGETYPE> edgeList = null;
            if (_Edges.ContainsKey(PartnerKey))
            {
                edgeList = _Edges[PartnerKey];
                edgeList.Remove(Link);

                if (edgeList.Count == 0)
                    _Edges.Remove(PartnerKey);
            }
        }

        public int Compare(Node<KEY, EDGETYPE> x, Node<KEY, EDGETYPE> y)
        {
            return this.CompareTo(y);
        }

        public int CompareTo(Node<KEY, EDGETYPE> other)
        {
            return this.Key.CompareTo(other.Key);
        }

        public override bool Equals(object other)
        {
            if (other as Node<KEY, EDGETYPE> != null)
            {
                return this.Key.Equals(((Node<KEY, EDGETYPE>)other).Key);
            }
            return base.Equals(other);
        }

        public bool Equals(Node<KEY, EDGETYPE> other)
        {
            if (object.ReferenceEquals(this, other))
                return true;

            if (other is null)
                return false;

            return this.Key.Equals(other.Key);
        }

        public override int GetHashCode()
        {
            return this.Key.GetHashCode();
        }


        public static bool operator ==(Node<KEY, EDGETYPE> A, Node<KEY, EDGETYPE> B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if (A is object)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(Node<KEY, EDGETYPE> A, Node<KEY, EDGETYPE> B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if (A is object)
                return !A.Equals(B);

            return true;
        }

    }
}
