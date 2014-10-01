using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

using Viking.Common; 

namespace Viking.UI.Controls
{
    public partial class ObjectTreeView : System.Windows.Forms.TreeView
    {
        private Dictionary<IUIObject, List<GenericTreeNode>> ObjectNodesTable = new Dictionary<IUIObject, List<GenericTreeNode>>(); 

        public ObjectTreeView()
        {
            InitializeComponent();

            // TODO: Add any initialization after the InitForm call
            this.Sorted = true;
//            this.ImageList = PlantMap.UI.SharedResources.SmallIconImageList;
            this.AllowDrop = true;
        }

        #region Node Hashtable Functions

        #region Private Node Hashtable Functions

        private void MapNode(GenericTreeNode Node, IUIObject Obj)
        {
            List<GenericTreeNode> NodeList = null;
            if (ObjectNodesTable.ContainsKey(Obj))
            {
                NodeList = ObjectNodesTable[Obj] as List<GenericTreeNode>;
            }
            else
                NodeList = new List<GenericTreeNode>(1);

            NodeList.Add(Node);

            ObjectNodesTable[Obj] = NodeList;
        }

        private void UnmapNode(GenericTreeNode Node)
        {
            IUIObject Obj = Node.Tag as IUIObject;
            if (ObjectNodesTable.ContainsKey(Obj))
            {
                List<GenericTreeNode> NodeList = ObjectNodesTable[Obj] as List<GenericTreeNode>;
                if (NodeList.Contains(Node))
                    NodeList.Remove(Node);

                if (NodeList.Count == 0)
                    ObjectNodesTable.Remove(Obj);
            }
        }

        #endregion

        #region Public Node Hashtable Functions

        public GenericTreeNode[] GetNodesForObject(IUIObject Obj)
        {
            if (Obj == null)
                return new GenericTreeNode[0];

            if (ObjectNodesTable.ContainsKey(Obj))
            {
                List<GenericTreeNode> NodeList = ObjectNodesTable[Obj] as List<GenericTreeNode>;
                return NodeList.ToArray();
            }

            return new GenericTreeNode[0];
        }

        public void RemoveNode(GenericTreeNode Node)
        {
//            IUIObject Obj = Node.Tag as IUIObject;
            UnmapNode(Node);
            Node.Remove();
        }

        #endregion
        #endregion

        public bool Busy
        {
            set
            {
                this.Enabled = !value;

                if (value == true)
                    this.Cursor = Cursors.WaitCursor;
                else
                    this.Cursor = Cursors.Default;
            }
        }

        public IUIObject SelectedObject
        {
            get
            {
                if (this.SelectedNode != null)
                    return this.SelectedNode.Tag as IUIObject;

                return null;
            }
            set
            {
                GenericTreeNode[] SelectedNodes = GetNodesForObject(value) as GenericTreeNode[];
                if (SelectedNodes.Length > 0)
                {
                    this.SelectedNode = SelectedNodes[0];
                }
            }
        }

        protected override void OnBeforeExpand(TreeViewCancelEventArgs e)
        {
            GenericTreeNode genNode = e.Node as GenericTreeNode;
            Debug.Assert(genNode != null);

            this.Busy = true;
            genNode.DoExpand();
            this.Busy = false;
        }

        /// <summary>
        /// Create a node for the given object and insert it in the tree under the parent node, 
        /// if parent node is null the new node is inserted at the root of the tree.
        /// </summary>
        /// <param name="Obj"></param>
        /// <param name="Parent"></param>
        /// <returns></returns>
        protected GenericTreeNode AddObject(IUIObject Obj, TreeNode Parent)
        {
            GenericTreeNode NewNode = Obj.CreateNode();

            if (Parent == null)
                this.Nodes.Add(NewNode);
            else
                Parent.Nodes.Add(NewNode);

            MapNode(NewNode, Obj);
            return NewNode;
        }

        #region DragDrop Code

        /// <summary>
        /// When a drag drop operation is occuring over the region of the control without any nodes we
        /// ask which types the control can insert at the root
        /// </summary>
        private Type[] _ValidDragDropTypes = new Type[0];
        public virtual Type[] ValidDragDropTypes
        {
            get
            {
                return _ValidDragDropTypes; 
            }
            set
            {
                _ValidDragDropTypes = value;
                if (_ValidDragDropTypes == null)
                    _ValidDragDropTypes = new Type[0];
            }
        }


        protected override void OnItemDrag(ItemDragEventArgs e)
        {
            base.OnItemDrag(e);

            TreeNode DragNode = e.Item as TreeNode;
            if (DragNode == null)
                return;

            IUIObject Obj = DragNode.Tag as IUIObject;
            if (Obj == null)
                return;

            UI.State.DragDropOrigin = new System.Drawing.Point(0, 0);
            UI.State.DragDropObject = Obj;
            DoDragDrop(Obj, DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
            UI.State.DragDropObject = null;
        }

        protected override void OnDragOver(System.Windows.Forms.DragEventArgs e)
        {
            base.OnDragOver(e);

            e.Effect = DragDropEffects.None;

            Point DragPoint = this.PointToClient(new Point(e.X, e.Y));
            TreeNode Node = this.GetNodeAt(DragPoint);
            IUIObject DragObject = UI.State.DragDropObject;

            //This means we are dragging over an empty region and we should ask the control which drag targets it supports
            if (Node == null)
            {
                //Find out if the object being dragged can be assigned to the control
                //This is a little reversed because in the rest of the code we ask the drag object who its parents
                //can be, in this code we as a control who its children can be.
                System.Type DragObjectType = DragObject.GetType();
                foreach (System.Type validType in this.ValidDragDropTypes)
                {
                    if (DragObjectType == validType)
                    {
                        e.Effect = DragDropEffects.Move;
                        return; 
                    }
                }

            }
            else
            {
                IUIObject Target = Node.Tag as IUIObject;
                if (Target == null)
                    return;

                //Can't drag onto ourselves
                if (Target == DragObject)
                    return;

                System.Type TargetType = Target.GetType();
                for (int i = 0; i < DragObject.AssignableParentTypes.Length; i++)
                {
                    if (DragObject.AssignableParentTypes[i].Equals(TargetType))
                    {
                        e.Effect = DragDropEffects.Move;
                        return;
                    }
                }
            }

           
        }

        protected override void OnDragDrop(System.Windows.Forms.DragEventArgs e)
        {
            Point DragPoint = this.PointToClient(new Point(e.X, e.Y));
            TreeNode DropNode = this.GetNodeAt(DragPoint);
            IUIObject DragObject = UI.State.DragDropObject;
            //We are dragging onto the control, but not a node in particular
            if (DropNode == null)
            {
                DragObject.SetParent(null);
            }
            else
            {

                IUIObject Target = DropNode.Tag as IUIObject;
                if (Target != null)
                {
                    if (Target != DragObject)
                    {
                        System.Type TargetType = Target.GetType();
                        for (int i = 0; i < DragObject.AssignableParentTypes.Length; i++)
                        {
                            if (DragObject.AssignableParentTypes[i].Equals(TargetType))
                            {
                                DragObject.SetParent(Target);
                                DragObject.Save();
                            }
                        }
                    }
                }
            }

            base.OnDragDrop(e);
        }
        #endregion


        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right && e.Button != MouseButtons.Left)
            {
                base.OnMouseDown(e);
            }

            TreeNode MouseNode = this.GetNodeAt(new Point(e.X, e.Y));

            if (e.Button == MouseButtons.Right)
            {
                if (MouseNode != null)
                {
                    IUIObject Obj = MouseNode.Tag as IUIObject;
                    this.ContextMenu = Obj.ContextMenu;
                }
                else
                {
                    //If we don't have a node, as the parent for a context menu
                    this.ContextMenu = Parent.ContextMenu;
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (MouseNode != null)
                {
                    UI.State.SelectedObject = MouseNode.Tag as IUIObject; 
                }
            }

            base.OnMouseDown(e); 
        }

        public void AddObjects(IEnumerable<IUIObject> Objects)
        {
            this.BeginUpdate();
           
            foreach (IUIObject Obj in Objects)
            {
                this.AddObject(Obj, null);
            }

            this.EndUpdate();
        }

        public void ClearObjects()
        {
            this.BeginUpdate();

            while(this.Nodes.Count > 0)
            {
                RemoveNode(this.Nodes[0] as GenericTreeNode); 
            }
            
            this.EndUpdate(); 
        }

        protected override void OnDoubleClick(System.EventArgs e)
        {
            Point P = PointToClient(Control.MousePosition);
            GenericTreeNode ClickNode = this.GetNodeAt(P) as GenericTreeNode;
            if (ClickNode != null)
            {
                ClickNode.OnDoubleClick();
            }
        }
    }
}
