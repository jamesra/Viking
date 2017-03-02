using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using System.Diagnostics;


namespace AnnotationVizLib
{
    public class MotifEdge : Edge<string>, IComparer<MotifEdge>, IComparable<MotifEdge>
    {
        public string SynapseType;
        
        /// <summary>
        /// A list of unique values indicating which structures have this type of connection
        /// </summary>
        public List<long> SourceStructIDs = new List<long>();

        /// <summary>
        /// A list of unique values indicating which structures have this type of connection
        /// </summary>
        public List<long> TargetStructIDs = new List<long>();

        public void AddEdgeInstance(long SourceStructID, long TargetStructID)
        {
            if (!SourceStructIDs.Contains(SourceStructID))
                SourceStructIDs.Add(SourceStructID);

            if (!TargetStructIDs.Contains(TargetStructID))
                TargetStructIDs.Add(TargetStructID); 

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

        public MotifNode(string key, IEnumerable<IStructure> value)
            : base(key)
        {
            this.Structures = new List<IStructure>();
            this.Structures.AddRange(value);
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
