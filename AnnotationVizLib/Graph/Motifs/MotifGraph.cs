using Viking.AnnotationServiceTypes.Interfaces;
using GraphLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib
{
    public class MotifEdge(string SourceKey, string TargetKey, string SynapseType) : Edge<string>(SourceKey, TargetKey, true), IComparer<MotifEdge>, IComparable<MotifEdge>
    {
        public string SynapseType = SynapseType;

        /// <summary>
        /// A list of unique values indicating which structures have this type of connection, and a list of the substructures making the connection
        /// </summary>
        public SortedList<long, SortedSet<long>> SourceStructIDs = [];

        /// <summary>
        /// A list of unique values indicating which structures have this type of connection, and a list of the substructures making the connection
        /// </summary>
        public SortedList<long, SortedSet<long>> TargetStructIDs = [];

        /// <summary>
        /// Number of parent cells for structure links
        /// </summary>
        public int SourceCellCount => SourceStructIDs.Count;

        /// <summary>
        /// Number of structure links
        /// </summary>
        public int SourceConnectionCount => SourceStructIDs.Values.Sum(links => links.Count);

        /// <summary>
        /// Number of parent cells for structure links
        /// </summary>
        public int TargetCellCount => TargetStructIDs.Count;

        /// <summary>
        /// Number of structure links
        /// </summary>
        public int TargetConnectionCount => TargetStructIDs.Values.Sum(links => links.Count);

        public void AddEdgeInstance(long SourceParentStructID, long SourceID, long TargetParentStructID, long TargetID)
        {
            if (!SourceStructIDs.ContainsKey(SourceParentStructID))
                SourceStructIDs.Add(SourceParentStructID, [SourceID]);
            else
                SourceStructIDs[SourceParentStructID].Add(SourceID);

            if (!TargetStructIDs.ContainsKey(TargetParentStructID))
                TargetStructIDs.Add(TargetParentStructID, [TargetID]);
            else
                TargetStructIDs[TargetParentStructID].Add(TargetID);
        }

        public override string ToString() => this.SourceNodeKey + " -> " + this.TargetNodeKey + " via " + this.SynapseType;

        public override int GetHashCode() => this.SourceNodeKey.GetHashCode();

        public int Compare(MotifEdge x, MotifEdge y)
        {
            if (x is null && y is null)
                return 0;

            if (x is null)
                return -1;
            if (y is null)
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

    public class MotifNode(string key, IEnumerable<IStructureReadOnly> value) : Node<string, MotifEdge>(key)
    {
        //Structures that belong to this node
        public List<IStructureReadOnly> Structures = [.. value];

        public int StructureCount => Structures.Count;

        /// <summary>
        /// The number of individual structure links
        /// </summary>
        public int OutputEdgesCount => this.Edges.Values.Sum(edges => edges.Where(e => e.SourceNodeKey == this.Key && e.Directional).Sum(e => e.SourceConnectionCount));

        /// <summary>
        /// The number of individual structure links
        /// </summary>
        public int InputEdgesCount => this.Edges.Values.Sum(edges => edges.Where(e => e.TargetNodeKey == this.Key && e.Directional).Sum(e => e.TargetConnectionCount));

        public int BidirectionalEdgesCount => this.Edges.Values.Sum(edges => edges.Where(e => !e.Directional).Sum(e => e.SourceConnectionCount));

        public override string ToString()
        {
            string Label = this.Key;

            foreach (IStructureReadOnly s in Structures)
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
            List<string> AlreadyAdded = [];

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
