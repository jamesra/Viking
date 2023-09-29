﻿using System.Collections.Generic;

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
        public SortedList<int, RegistrationTreeNode> Nodes = new SortedList<int, RegistrationTreeNode>();

        /// <summary>
        /// Nodes with no known parents
        /// </summary>
        public SortedList<int, RegistrationTreeNode> RootNodes = new SortedList<int, RegistrationTreeNode>();

        public void AddPair(int ControlSection, int MappedSection)
        {
            RegistrationTreeNode ControlNode = null;
            if (Nodes.ContainsKey(ControlSection))
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="TList"></param>
        /// <param name="ValidSections">Optional. Restrict tree to contain only section numbers in the valid section list.</param>
        /// <returns></returns>
        public static RegistrationTree Build(SortedList<int, Geometry.ITransform> TList, IList<int> ValidSections = null)
        {
            SortedSet<int> ValidSectionsLookup = null;
            if (ValidSections != null)
            {
                ValidSectionsLookup = new SortedSet<int>(ValidSections);
            }
            //Create a registration chain so we know what order to register the sections in
            RegistrationTree tree = new RegistrationTree();
            foreach (int iSection in TList.Keys)
            {
                Geometry.ITransform trans = TList[iSection];
                if (!(((Geometry.ITransformInfo)trans)?.Info is Geometry.Transforms.StosTransformInfo info))
                    continue;

                if (ValidSectionsLookup != null && !ValidSectionsLookup.Contains(info.MappedSection))
                    continue;

                tree.AddPair(info.ControlSection, info.MappedSection);
            }

            return tree;
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
