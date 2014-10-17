using System;
using System.Collections.Generic;
using System.Collections.Specialized; 
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI.Controls;
using WebAnnotation;
using WebAnnotation.ViewModel;
using WebAnnotationModel; 

namespace WebAnnotation.UI
{
    [ExtensionTabAttribute("Structure Types", TABCATEGORY.ACTION)]
    public partial class StructureTypesTree : Viking.UI.BaseClasses.DockingTreeControl
    {
        public StructureTypesTree()
        {
            InitializeComponent();

            this.Title = "Structure Types";

            Store.StructureTypes.OnCollectionChanged += this.OnStructureTypeCollectionChanged;
        }

        protected override void InitializeTree()
        {
            ICollection<StructureTypeObj> listTypes = Store.StructureTypes.rootObjects.Values;
            List<StructureType> listRootTypes = new List<StructureType>(listTypes.Count);
            foreach (StructureTypeObj type in listTypes)
            {
                StructureType rootType = new StructureType(type);
                listRootTypes.Add(rootType);
            }

            Tree.AddObjects(listRootTypes.ToArray());
        }

        protected void OnStructureTypeCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if(InvokeRequired)
            {
                //Ensure UI controls are updated in main thread
                this.Invoke( new Action(() => this.OnStructureTypeCollectionChanged(sender, args)));
                return; 
            }
            else
            { 
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach (object o in args.NewItems)
                        {
                            StructureTypeObj newTypeObj = o as StructureTypeObj;
                            if(newTypeObj != null)
                            {
                                StructureType newType = new StructureType(newTypeObj); 
                                if (newType.Parent != null)
                                {
                                    GenericTreeNode[] Nodes = Tree.GetNodesForObject(newType.Parent);
                                    foreach (GenericTreeNode node in Nodes)
                                    {
                                        node.UpdateChildNodes();
                                    }
                                }
                                else
                                    Tree.AddObjects(new IUIObject[] { newType });
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:

                        foreach (object o in args.OldItems)
                        {
                            StructureTypeObj oldTypeObj = o as StructureTypeObj;
                            if (oldTypeObj != null)
                            {
                                StructureType oldType = new StructureType(oldTypeObj);
                                Viking.UI.Controls.GenericTreeNode[] nodes = Tree.GetNodesForObject(oldType);
                                foreach (Viking.UI.Controls.GenericTreeNode node in nodes)
                                {
                                    Tree.RemoveNode(node);
                                }
                            }
                        }
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        foreach (object o in args.OldItems)
                        {
                            StructureTypeObj TypeObj = o as StructureTypeObj;
                            if (TypeObj != null)
                            {
                                StructureType t = new StructureType(TypeObj);
                                if (t.Parent != null)
                                {
                                    Viking.UI.Controls.GenericTreeNode[] nodes = Tree.GetNodesForObject(t);
                                    foreach (Viking.UI.Controls.GenericTreeNode node in nodes)
                                    {
                                        node.UpdateChildNodes();
                                    }
                                }
                            }
                        }
                        break; 

                    case NotifyCollectionChangedAction.Reset:
                        foreach (object o in args.OldItems)
                        {
                            StructureTypeObj TypeObj = o as StructureTypeObj;
                            if (TypeObj != null)
                            {
                                StructureType t = new StructureType(TypeObj);
                                if (t.Parent != null)
                                {
                                    Viking.UI.Controls.GenericTreeNode[] nodes = Tree.GetNodesForObject(t);
                                    foreach (Viking.UI.Controls.GenericTreeNode node in nodes)
                                    {
                                        node.UpdateChildNodes();
                                    }
                                }
                            }
                        }
                        break;

                }
            }
        }

        private void Tree_MouseDown(object sender, MouseEventArgs e)
        {
            
            if (e.Button == MouseButtons.Right)
            {
                TreeNode node = this.Tree.GetNodeAt(e.Location);

                if (node == null)
                {
                    Viking.UI.State.SelectedObject = null; 
                    ContextMenu menu = new ContextMenu();

                    MenuItem menuItem = new MenuItem("New", OnNewStructureType);

                    menu.MenuItems.Add(menuItem);

                    this.ContextMenu = menu;

                    this.ContextMenu.Show(this, e.Location);
                }
            }
        }

        private void OnNewStructureType(object sender, EventArgs e)
        {
            StructureTypeObj newTypeObj = new StructureTypeObj();
            StructureType newType = new StructureType(newTypeObj);

            if (newType.ShowPropertiesDialog(this.ParentForm) == DialogResult.OK)
            {
                newTypeObj = Store.StructureTypes.Add(newTypeObj);
                Store.StructureTypes.Save();

                Viking.UI.State.SelectedObject = new StructureType(newTypeObj); 
            }
        }
    }
}
