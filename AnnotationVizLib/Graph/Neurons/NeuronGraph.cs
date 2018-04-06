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
        
        public double TotalSourceArea
        {
            get
            {
                if (!Attributes.ContainsKey("TotalSourceArea"))
                {
                    return 0;
                }

                return System.Convert.ToDouble(Attributes["TotalSourceArea"]);
            }
            set
            {
                Attributes["TotalSourceArea"] = value;
            }
        }

        public double TotalTargetArea
        {
            get
            {
                if (!Attributes.ContainsKey("TotalTargetArea"))
                {
                    return 0;
                }

                return System.Convert.ToDouble(Attributes["TotalTargetArea"]);
            }
            set
            {
                Attributes["TotalTargetArea"] = value;
            }
        }

        public double MinZ
        {
            get
            {
                if (!Attributes.ContainsKey("MinZ"))
                {
                    return double.MaxValue;
                }

                return System.Convert.ToDouble(Attributes["MinZ"]);
            }
            set
            { 
                Attributes["MinZ"] = value;
            }
        }

        public double MaxZ
        {
            get
            {
                if (!Attributes.ContainsKey("MaxZ"))
                {
                    return double.MinValue;
                }

                return System.Convert.ToDouble(Attributes["MaxZ"]);
            }
            set
            { 
                Attributes["MaxZ"] = value;
            }
        }

        public override double Weight
        {
            get
            {
                return (double)Links.Count();
            }
        }

        public ulong[] SourceIDs
        {
            get
            {
                return Links.Select(l => l.SourceID).ToArray();
            }
        }

        public ulong[] TargetIDs
        {
            get
            {
                return Links.Select(l => l.TargetID).ToArray();
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
        
        public IEnumerable<ulong> EdgeSourceChildStructureIDs { get { return this.Edges.Values.SelectMany(e => e.SelectMany(s => s.SourceIDs)); } }
        public IEnumerable<ulong> EdgeTargetChildStructureIDs { get { return this.Edges.Values.SelectMany(e => e.SelectMany(s => s.TargetIDs)); } }

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
