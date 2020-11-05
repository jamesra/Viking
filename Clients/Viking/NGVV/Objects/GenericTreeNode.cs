using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Viking.Common;
using Viking.Common.UI;

namespace Viking.UI.Controls
{
    [Serializable]
    public class GenericTreeNode : System.Windows.Forms.TreeNode
    {
        public new ObjectTreeView TreeView
        {
            get
            {
                return base.TreeView as ObjectTreeView;
            }
        }

        public new GenericTreeNode Parent
        {
            get
            {
                return base.Parent as GenericTreeNode;
            }
        }


        public IUIObject Object
        {
            get
            {
                return this.Tag as IUIObject;
            }
        }

        /// <summary>
        /// The first time we expand the node we set this value to true. Before then this node has a dummy child node under it. 
        /// This allows the user to expand the node, and saves us the trouble of looking up the actual children
        /// </summary>
        private bool HasExpanded = false;

        public GenericTreeNode(IUIObject Obj)
            : base(Obj.ToString(), Obj.TreeImageIndex, Obj.TreeSelectedImageIndex)
        {
            this.Tag = Obj;
            Obj.AfterSave += new EventHandler(this.OnObjectSave);
            Obj.BeforeDelete += new EventHandler(this.OnObjectDelete);
            Obj.ChildChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(this.OnChildChanged);

            //Add a dummy node only if the object could have child nodes
            if (this.CanHaveChildren)
                this.Nodes.Add("Temporary Child Node");
        }


        protected GenericTreeNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            throw new NotImplementedException();
        }



        protected virtual void OnObjectSave(object sender, System.EventArgs e)
        {
            //Adjust node name and icons to ensure correctness
            string NewName = Object.ToString();
            this.Text = NewName;
            //TODO: Add support for images
            //			this.ImageIndex = Object.TreeImageIndex;
            //			this.SelectedImageIndex = Object.TreeSelectedImageIndex;
        }

        protected virtual void OnChildChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs Args)
        {
            Trace.WriteLine(this.Object.ToString() + " Child Changed " + Args.Action.ToString(), "UI");
            this.UpdateChildNodes();
        }

        protected virtual void OnObjectDelete(object sender, System.EventArgs e)
        {
            if (this.TreeView != null)
                this.TreeView.RemoveNode(this);
        }

        protected virtual GenericTreeNode[] GetChildNodes()
        {
            Trace.WriteLine("Get Child Nodes for " + Object.ToString(), "UI");

            List<GenericTreeNode> NodeList = new List<GenericTreeNode>();

            System.Reflection.PropertyInfo[] Properties = GenericTreeNode.GetPropertiesForType(this.Object.GetType());
            foreach (System.Reflection.PropertyInfo Prop in Properties)
            {
                IUIObject[] Children = Prop.GetValue(Object, null) as IUIObject[];
                foreach (IUIObject Child in Children)
                {
                    Trace.WriteLine("\t" + Child.ToString(), "UI");
                    GenericTreeNode NewNode = Child.CreateNode();
                    NodeList.Add(NewNode);
                }
            }

            return NodeList.ToArray();
        }

        public void UpdateChildNodes()
        {
            //Keep a list of all the objects represented in the tree, and the nodes that represent them.
            //As we verify that these nodes are still children we'll remove them from this list
            if (this.HasExpanded == false)
                this.Nodes.Clear();

            Dictionary<IUIObject, GenericTreeNode> NodesToDelete = new Dictionary<IUIObject, GenericTreeNode>();
            foreach (TreeNode Node in this.Nodes)
            {
                NodesToDelete.Add(Node.Tag as IUIObject, (GenericTreeNode)Node);
            }

            GenericTreeNode[] ChildNodes = GetChildNodes();
            foreach (GenericTreeNode ChildNode in ChildNodes)
            {
                //Node already exists, don't add another node
                if (NodesToDelete.ContainsKey(ChildNode.Object))
                {
                    NodesToDelete.Remove(ChildNode.Object);
                }
                //Node doesn't exist, add the new node
                else
                {
                    this.Nodes.Add(ChildNode);

                    //If this Node is visible, be sure to expand our child nodes
                    if (this.IsExpanded)
                        ChildNode.UpdateChildNodes();
                }
            }

            //Remove all Nodes who are not accounted for and therefore no longer our children
            foreach (GenericTreeNode Node in NodesToDelete.Values)
            {
                TreeView.RemoveNode(Node);
            }

            this.HasExpanded = true;
        }

        public void DoExpand()
        {
            //The first time this is called is when the child is expanded for the first time. 
            if (this.HasExpanded == false)
            {
                UpdateChildNodes();
                return;
            }
        }

        /// <summary>
        /// Default behaviour is to show the object properties if the node has no children. 
        /// If it does have children the underlying UI code will expand the node
        /// </summary>
        public virtual void OnDoubleClick()
        {
            if (this.Nodes.Count == 0)
                Object.ShowProperties();
        }

        protected virtual bool CanHaveChildren
        {
            get
            {
                PropertyInfo[] Props = GenericTreeNode.GetPropertiesForType(this.Object.GetType());
                if (Props != null && Props.Length > 0)
                    return true;
                return false;
            }
        }

        #region Static Code

        /// <summary>
        /// Hashtable of PropertyInfo[] by System.Type
        /// </summary>
        static private System.Collections.Hashtable _TreeViewVisibleChildPropertiesByType = new System.Collections.Hashtable();

        static protected System.Reflection.PropertyInfo[] GetPropertiesForType(System.Type T)
        {
            if (_TreeViewVisibleChildPropertiesByType.Contains(T))
                return (PropertyInfo[])_TreeViewVisibleChildPropertiesByType[T];

            List<PropertyInfo> propInfoList = new List<PropertyInfo>();

            System.Reflection.PropertyInfo[] Properties = T.GetProperties();
            foreach (System.Reflection.PropertyInfo Prop in Properties)
            {
                //Find out if this property lists child objects
                object[] Attribs = Prop.GetCustomAttributes(typeof(ThisToManyRelationAttribute), true);
                if (Attribs.Length == 0)
                    continue;

                System.Type ChildType = Prop.PropertyType.GetElementType();
                Attribs = ChildType.GetCustomAttributes(typeof(TreeViewVisibleAttribute), false);
                if (Attribs.Length == 0)
                {
                    Trace.WriteLine("Not adding type " + ChildType.ToString(), "UI");
                    continue;
                }

                propInfoList.Add(Prop);
            }

            PropertyInfo[] propInfoArray = propInfoList.ToArray();
            _TreeViewVisibleChildPropertiesByType.Add(T, propInfoArray);

            return propInfoArray;
        }

        #endregion
    }
}
