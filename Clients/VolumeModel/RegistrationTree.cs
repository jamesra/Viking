using System.Collections.Generic;

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
        public SortedList<int, RegistrationTreeNode> Nodes = [];

        /// <summary>
        /// Nodes with no known parents
        /// </summary>
        public SortedList<int, RegistrationTreeNode> RootNodes = [];

        /// <summary>
        /// Stos pair: ControlSection is the parent/reference (volume side); MappedSection is the child being registered.
        /// </summary>
        public void AddPair(int ControlSection, int MappedSection)
        {
            if (!Nodes.TryGetValue(ControlSection, out RegistrationTreeNode ControlNode))
            {
                ControlNode = new RegistrationTreeNode(ControlSection);
                Nodes.Add(ControlNode.SectionNumber, ControlNode);
                RootNodes.Add(ControlNode.SectionNumber, ControlNode);
            }

            ControlNode.Children.Add(MappedSection);

            if (Nodes.TryGetValue(MappedSection, out RegistrationTreeNode MappedNode))
            {
                MappedNode.SetParent(new int?(ControlSection));
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
                ValidSectionsLookup = [.. ValidSections];
            }
            //Create a registration chain so we know what order to register the sections in
            RegistrationTree tree = new();
            foreach (int iSection in TList.Keys)
            {
                Geometry.ITransform trans = TList[iSection];
                if (((Geometry.ITransformInfo)trans)?.Info is not Geometry.Transforms.StosTransformInfo info)
                    continue;

                if (ValidSectionsLookup != null && !ValidSectionsLookup.Contains(info.MappedSection))
                    continue;

                tree.AddPair(info.ControlSection, info.MappedSection);
            }

            return tree;
        }
    }

    class RegistrationTreeNode(int sectionNumber)
    {
        public int? Parent = new int?();
        public readonly int SectionNumber = sectionNumber;
        public List<int> Children = [];

        public RegistrationTreeNode(int sectionNumber, int parentSection) : this(sectionNumber)
        {
            Parent = new int?(sectionNumber);
        }

        public override int GetHashCode() => SectionNumber;

        /// <summary>
        /// We are a root node if we have no parent
        /// </summary>
        bool IsRoot => !Parent.HasValue;

        public void SetParent(int? parentSection) => Parent = parentSection;

        void AddChild(int childSection)
        {
            Children.Add(childSection);
            Children.Sort();
        }
    }
}
