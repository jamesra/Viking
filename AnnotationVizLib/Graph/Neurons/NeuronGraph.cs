using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using System.Threading.Tasks; 
using System.Diagnostics;

namespace AnnotationVizLib
{
    public class StructureLinkComparer : Comparer<IStructureLink>
    {
        public override int Compare(IStructureLink x, IStructureLink y)
        {
            if (object.ReferenceEquals(x, y))
                return 0;

            bool XIsNull = (object)x == null;
            bool YIsNull = (object)y == null;

            if (XIsNull)
                return -1;
            else if (YIsNull)
                return 1;

            int SourceCompare = x.SourceID.CompareTo(y.SourceID);
            if (SourceCompare != 0)
                return SourceCompare;
            else
            {
                int TargetCompare = x.TargetID.CompareTo(y.TargetID);
                return TargetCompare;
            }
        }         
    }

    public class NeuronEdge : Edge<long>, IComparer<NeuronEdge>, IComparable<NeuronEdge>, IEquatable<NeuronEdge>
    {
        public string SynapseType;

        /// <summary>
        /// List of child structures involved in the link
        /// </summary>
        public SortedSet<IStructureLink> Links = new SortedSet<IStructureLink>(new StructureLinkComparer());

        /// <summary>
        /// A collection of additional attributes that have been added to the node
        /// </summary>
        public Dictionary<string, object> Attributes = new Dictionary<string, object>();

        public double TotalSourceArea;
        public double TotalTargetArea;

        public override float Weight
        {
            get
            {
                return (float)Links.Count();
            }
        }

        public NeuronEdge(long SourceKey, long TargetKey, IStructureLink Link, string SynapseType)
            : base(SourceKey, TargetKey, Link.Directional)
        {
            this.Links.Add(Link);
            this.SynapseType = SynapseType;
        }

        public void AddLink(IStructureLink link)
        {
            Debug.Assert(!Links.Contains(link));
            Debug.Assert(this.Directional == link.Directional);

            Links.Add(link);
        }

        public string PrintChildLinks()
        {
            string output = "";
            bool first = true;
            foreach(IStructureLink link in Links)
            {
                if (!first)
                {
                    output += ", ";
                }

                first = false;

                output += link.SourceID.ToString() + " -> " + link.TargetID.ToString();
            }
            
            return output;
        }

        public override string ToString()
        {
            return this.SourceNodeKey.ToString() + "-" + this.TargetNodeKey.ToString() + " via " + this.SynapseType + " " + PrintChildLinks();
        }

        public int Compare(NeuronEdge x, NeuronEdge y)
        {
            int comparison = base.Compare(x, y);
            if (comparison != 0)
                return comparison;

            if((object)x != null && (object)y != null)
            {
                return string.Compare(x.SynapseType, y.SynapseType);
            }
            else
                return comparison;
        }

        public int CompareTo(NeuronEdge other)
        {
            int comparison = base.CompareTo(other);
            if (comparison != 0)
                return comparison;

            if((object)other != null)
            {
                return this.SynapseType.CompareTo(other.SynapseType);
            }
            else
                return comparison;
        }

        public bool Equals(NeuronEdge other)
        {
            bool baseEquals = base.Equals(other);
            if(baseEquals && ((object)other != null))
            {
                return this.SynapseType.Equals(other.SynapseType);
            }

            return false;
        }
    }

    public class NeuronNode : Node<long, NeuronEdge>
    {
        //Structure this node represents
        public IStructure Structure;

        /// <summary>
        /// A collection of additional attributes that have been added to the node
        /// </summary>
        public Dictionary<string, object> Attributes = new Dictionary<string, object>();

        public NeuronNode(long key, IStructure value)
            : base(key)
        {
            this.Structure = value;
            
        }

        public override string ToString()
        {
            return this.Key.ToString() + " : " + Structure.Label;
        }
    }

    public class NeuronGraph : Graph<long, NeuronNode, NeuronEdge>
    { 
       
    }

}
