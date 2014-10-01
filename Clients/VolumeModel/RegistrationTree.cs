using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Viking.VolumeModel
{
    /// <summary>
    /// The registration tree tracks which sections are mapped to each other.  When calculating the section to volume transform we begin with the 
    /// root nodes and register down the tree until registration is complete
    /// </summary>
    class RegistrationTree
    {
        /// <summary>
        /// Nodes in the tree
        /// </summary>
        public SortedList<int, RegistrationTreeNode> Nodes =new SortedList<int,RegistrationTreeNode>();

        /// <summary>
        /// Nodes with no known parents
        /// </summary>
        public SortedList<int, RegistrationTreeNode> RootNodes = new SortedList<int, RegistrationTreeNode>(); 

        public void AddPair(int ControlSection, int MappedSection)
        {
            RegistrationTreeNode ControlNode = null;
            if(Nodes.ContainsKey(ControlSection))
            {
                ControlNode = Nodes[ControlSection];
            }
            else
            {
                ControlNode = new RegistrationTreeNode(ControlSection);
                Nodes.Add(ControlNode.SectionNumber, ControlNode);
                RootNodes.Add(ControlNode.SectionNumber, ControlNode); 
            }

            ControlNode.Children.Add(MappedSection); 

            RegistrationTreeNode MappedNode = null;
            if (Nodes.ContainsKey(MappedSection))
            {
                MappedNode = Nodes[MappedSection];
                MappedNode.SetParent(new int?(ControlSection));
                if (RootNodes.ContainsKey(MappedNode.SectionNumber))
                    RootNodes.Remove(MappedNode.SectionNumber);
            }
            else
            {
                MappedNode = new RegistrationTreeNode(MappedSection, ControlSection);
                Nodes.Add(MappedNode.SectionNumber, MappedNode); 
            }
        }
    }

    class RegistrationTreeNode
    {
        public int? Parent = new int?(); 
        public readonly int SectionNumber;
        public List<int> Children = new List<int>();

        public RegistrationTreeNode(int sectionNumber)
        {
            SectionNumber = sectionNumber; 
        }

        public RegistrationTreeNode(int sectionNumber, int parentSection) : this(sectionNumber)
        {
            Parent = new int?(sectionNumber); 
        }

        override public int GetHashCode()
        {
            return SectionNumber;
        }

        /// <summary>
        /// We are a root node if we have no parent
        /// </summary>
        bool IsRoot
        {
            get { return !Parent.HasValue; }
        }

        public void SetParent(int? parentSection)
        {
            Parent = parentSection; 
        }

        void AddChild(int childSection)
        {
            Children.Add(childSection);
            Children.Sort(); 
        }
    }
}
