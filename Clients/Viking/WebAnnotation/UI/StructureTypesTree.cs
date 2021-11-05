using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI.Controls;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

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
            ICollection<StructureTypeObj> listTypes = Store.StructureTypes.GetObjectsByIDs(Store.StructureTypes.RootObjects, true);
            List<StructureType> listRootTypes = new List<StructureType>(listTypes.Count);
            foreach (StructureTypeObj type in listTypes)
            {
                StructureType rootType = new StructureType(type);
                listRootTypes.Add(rootType);
            }

            Tree.AddObjects(listRootTypes.ToArray());
        }

        protected void UpdateNodeChildren(StructureType obj)
        {
            GenericTreeNode[] Nodes = Tree.GetNodesForObject(obj);
            foreach (GenericTreeNode node in Nodes)
            {
                node.UpdateChildNodes();
            }
        }

        protected void AddNewObjects(ICollection<StructureTypeObj> added)
        {
            //Find all of the root objects
            List<StructureType> listRoots = new List<StructureType>(added.Count);
            Dictionary<long, StructureType> listParents = new Dictionary<long, StructureType>(added.Count);

            foreach (StructureTypeObj newTypeObj in added)
            {
                StructureType newType = new StructureType(newTypeObj);
                if (!newTypeObj.ParentID.HasValue)
                {
                    if (!Tree.Contains(newType))
                        listRoots.Add(newType);
                    else if (!listParents.ContainsKey(newType.ID))
                        listParents.Add(newType.ID, newType);
                }
                else
                {
                    if (!listParents.ContainsKey(newTypeObj.ParentID.Value))
                        listParents.Add(newTypeObj.ParentID.Value, newType.Parent);
                }
            }

            Tree.Invoke(new Action(() => Tree.AddObjects(listRoots)));

            foreach (StructureType parent in listParents.Values)
            {
                Tree.Invoke(new Action(() => UpdateNodeChildren(parent)));
            }
        }

        protected void OnStructureTypeCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (InvokeRequired)
            {
                //Ensure UI controls are updated in main thread
                this.Invoke(new Action(() => this.OnStructureTypeCollectionChanged(sender, args)));
                return;
            }
            else
            {
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:

                        List<StructureTypeObj> newItems = new List<StructureTypeObj>(args.NewItems.Count);
                        //I'd rather case, but can't figure it out with IList interface...
                        foreach (object obj in args.NewItems)
                        {
                            newItems.Add((StructureTypeObj)obj);
                        }

                        AddNewObjects(newItems);
                        /*
                        foreach (object o in args.NewItems)
                        {
                            StructureTypeObj newTypeObj = o as StructureTypeObj;
                            if(newTypeObj != null)
                            {
                                StructureType newType = new StructureType(newTypeObj); 
                                if (newType.Parent != null)
                                {
                                    UpdateNodeChildren(newType.Parent);
                                }
                                else
                                    Tree.AddObjects(new IUIObject[] { newType });
                            }
                        }
                         */
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
                                    Tree.Invoke(new Action(() => Tree.RemoveNode(node)));
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
                                    UpdateNodeChildren(t);
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
                                    UpdateNodeChildren(t);
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
                try
                {
                    newTypeObj = Store.StructureTypes.Create(newTypeObj);
                    Store.StructureTypes.Save();
                }
                catch (System.ServiceModel.FaultException ex)
                {
                    AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                }

                Viking.UI.State.SelectedObject = new StructureType(newTypeObj);
            }
        }
    }
}
