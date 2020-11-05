using Annotation.Interfaces;
using GraphLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib
{
    public class MotifEdge : Edge<string>, IComparer<MotifEdge>, IComparable<MotifEdge>
    {
        public string SynapseType;

        /// <summary>
        /// A list of unique values indicating which structures have this type of connection, and a list of the substructures making the connection
        /// </summary>
        public SortedList<long, SortedSet<long>> SourceStructIDs = new SortedList<long, SortedSet<long>>();

        /// <summary>
        /// A list of unique values indicating which structures have this type of connection, and a list of the substructures making the connection
        /// </summary>
        public SortedList<long, SortedSet<long>> TargetStructIDs = new SortedList<long, SortedSet<long>>();

        /// <summary>
        /// Number of parent cells for structure links
        /// </summary>
        public int SourceCellCount { get { return SourceStructIDs.Count; } }

        /// <summary>
        /// Number of structure links
        /// </summary>
        public int SourceConnectionCount
        { get { return SourceStructIDs.Values.Sum(links => links.Count); } }

        /// <summary>
        /// Number of parent cells for structure links
        /// </summary>
        public int TargetCellCount { get { return TargetStructIDs.Count; } }

        /// <summary>
        /// Number of structure links
        /// </summary>
        public int TargetConnectionCount
        { get { return TargetStructIDs.Values.Sum(links => links.Count); } }

        public void AddEdgeInstance(long SourceParentStructID, long SourceID, long TargetParentStructID, long TargetID)
        {
            if (!SourceStructIDs.ContainsKey(SourceParentStructID))
                SourceStructIDs.Add(SourceParentStructID, new SortedSet<long>(new long[] { SourceID }));
            else
                SourceStructIDs[SourceParentStructID].Add(SourceID);

            if (!TargetStructIDs.ContainsKey(TargetParentStructID))
                TargetStructIDs.Add(TargetParentStructID, new SortedSet<long>(new long[] { TargetID }));
            else
                TargetStructIDs[TargetParentStructID].Add(TargetID);
        }

        public MotifEdge(string SourceKey, string TargetKey, string SynapseType)
            : base(SourceKey, TargetKey, true)
        {
            this.SynapseType = SynapseType;
        }

        public override string ToString()
        {
            return this.SourceNodeKey + " -> " + this.TargetNodeKey + " via " + this.SynapseType;
        }

        public override int GetHashCode()
        {
            return this.SourceNodeKey.GetHashCode();
        }

        public int Compare(MotifEdge x, MotifEdge y)
        {
            if (x == null && y == null)
                return 0;

            if (x == null)
                return -1;
            if (y == null)
                return 1;

            return x.CompareTo(y);
        }

        public int CompareTo(MotifEdge other)
        {
            int SourceComparison = this.SourceNodeKey.CompareTo(other.SourceNodeKey);
            int TargetComparison = this.TargetNodeKey.CompareTo(other.TargetNodeKey);
            int SynapseTypeComparison = this.SynapseType.CompareTo(other.SynapseType);

            if (SourceComparison == 0 && TargetComparison == 0)
                return SynapseTypeComparison;

            if (SourceComparison != 0)
                return SourceComparison;

            return TargetComparison;
        }
    }

    public class MotifNode : Node<string, MotifEdge>
    {
        //Structures that belong to this node
        public List<IStructure> Structures;

        public int StructureCount
        {
            get { return Structures.Count; }
        }

        public MotifNode(string key, IEnumerable<IStructure> value)
            : base(key)
        {
            this.Structures = new List<IStructure>();
            this.Structures.AddRange(value);
        }

        /// <summary>
        /// The number of individual structure links
        /// </summary>
        public int OutputEdgesCount
        {
            get
            {
                return this.Edges.Values.Sum(edges => edges.Where(e => e.SourceNodeKey == this.Key && e.Directional).Sum(e => e.SourceConnectionCount));
            }
        }

        /// <summary>
        /// The number of individual structure links
        /// </summary>
        public int InputEdgesCount
        {
            get
            {
                return this.Edges.Values.Sum(edges => edges.Where(e => e.TargetNodeKey == this.Key && e.Directional).Sum(e => e.TargetConnectionCount));
            }
        }

        public int BidirectionalEdgesCount
        {
            get
            {
                return this.Edges.Values.Sum(edges => edges.Where(e => !e.Directional).Sum(e => e.SourceConnectionCount));
            }
        }

        public override string ToString()
        {
            string Label = this.Key;

            foreach (IStructure s in Structures)
            {
                Label = Label + ", " + s.ID.ToString();
            }

            return Label;
        }
    }


    public class MotifGraph : Graph<string, MotifNode, MotifEdge>
    {
        public MotifGraph()
        {

        }

        public override string ToString()
        {
            List<string> AlreadyAdded = new List<string>();

            foreach (MotifEdge e in this.Edges.Values)
            {
                string EdgeLabel = e.ToString();
                if (!AlreadyAdded.Contains(EdgeLabel))
                {

                    AlreadyAdded.Add(EdgeLabel);
                }
            }

            AlreadyAdded.Sort();

            string Label = "";
            foreach (string l in AlreadyAdded)
            {
                Label = Label + l + '\n';
            }

            return Label;
        }


    }
}
