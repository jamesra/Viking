using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI.Controls;
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

            Title = "Structure Types";

            Tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            Tree.DrawNode += Tree_DrawNode;

            Store.StructureTypes.OnCollectionChanged += OnStructureTypeCollectionChanged;
        }

        protected override void InitializeTree()
        {
            ICollection<StructureTypeObj> listTypes = Store.StructureTypes.GetObjectsByIDs(Store.StructureTypes.RootObjects, true);
            List<StructureType> listRootTypes = new(listTypes.Count);
            foreach (StructureTypeObj type in listTypes)
            {
                StructureType rootType = new(type);
                listRootTypes.Add(rootType);
            }

            Tree.AddObjects([.. listRootTypes]);
        }

        protected void UpdateNodeChildren(StructureType obj)
        {
            GenericTreeNode[] Nodes = Tree.GetNodesForObject(obj);
            foreach (GenericTreeNode node in Nodes)
            {
                BeginInvoke(new Action(() => node.UpdateChildNodes()));
            }
        }

        protected void AddNewObjects(ICollection<StructureTypeObj> added)
        {
            //Find all of the root objects
            List<StructureType> listRoots = new(added.Count);
            Dictionary<long, StructureType> listParents = new(added.Count);

            foreach (StructureTypeObj newTypeObj in added)
            {
                StructureType newType = new(newTypeObj);
                if (!newTypeObj.ParentID.HasValue)
                {
                    if (!Tree.Contains(newType))
                    {
                        listRoots.Add(newType);
                    }
                    else if (!listParents.ContainsKey(newType.ID))
                    {
                        listParents.Add(newType.ID, newType);
                    }
                }
                else
                {
                    if (!listParents.ContainsKey(newTypeObj.ParentID.Value))
                    {
                        listParents.Add(newTypeObj.ParentID.Value, newType.Parent);
                    }
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
                Invoke(new Action(() => OnStructureTypeCollectionChanged(sender, args)));
                return;
            }
            else
            {
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:

                        List<StructureTypeObj> newItems = new(args.NewItems.Count);
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
                                StructureType oldType = new(oldTypeObj);
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
                                StructureType t = new(TypeObj);
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
                                StructureType t = new(TypeObj);
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

        /// <summary>Width of the column at the right edge of the tree that holds the eyes.</summary>
        private const int EyeColumnWidth = 26;

        /// <summary>Children of a structure type, for hiding or showing a type together with its descendants.</summary>
        private static readonly Func<long, IEnumerable<long>> ChildTypeIds = HiddenStructureTypes.ChildTypeIds;

        private void Tree_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            // The tree draws the node text, selection and expand glyphs; the eye is added in the column on the right.
            e.DrawDefault = true;

            if (e.Node?.Tag is not StructureType type || e.Bounds.IsEmpty)
                return;

            Rectangle eyeCell = new(Tree.ClientSize.Width - EyeColumnWidth, e.Bounds.Top, EyeColumnWidth, e.Bounds.Height);
            using SolidBrush background = new(Tree.BackColor);
            e.Graphics.FillRectangle(background, eyeCell);
            DrawEye(e.Graphics, eyeCell, HiddenStructureTypeSet.Current.StateOf(type.ID, ChildTypeIds));
        }

        /// <summary>
        /// Draws an eye: open when the type is shown, struck through and grey when hidden, and with a half-filled pupil
        /// when only some of its descendants are hidden.
        /// </summary>
        private void DrawEye(Graphics g, Rectangle cell, StructureTypeEyeState state)
        {
            System.Drawing.Drawing2D.SmoothingMode oldMode = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color ink = state == StructureTypeEyeState.Hidden ? SystemColors.GrayText : SystemColors.ControlText;
            float width = 14f;
            float height = Math.Min(9f, cell.Height - 4f);
            float left = cell.Left + ((cell.Width - width) / 2f);
            float top = cell.Top + ((cell.Height - height) / 2f);
            float midY = top + (height / 2f);

            using Pen pen = new(ink, 1.4f);
            using System.Drawing.Drawing2D.GraphicsPath outline = new();
            outline.AddBezier(left, midY, left + (width * 0.3f), top - (height * 0.3f), left + (width * 0.7f), top - (height * 0.3f), left + width, midY);
            outline.AddBezier(left + width, midY, left + (width * 0.7f), top + (height * 1.3f), left + (width * 0.3f), top + (height * 1.3f), left, midY);
            g.DrawPath(pen, outline);

            float pupil = height * 0.6f;
            RectangleF pupilRect = new(left + ((width - pupil) / 2f), midY - (pupil / 2f), pupil, pupil);
            using SolidBrush fill = new(ink);
            if (state == StructureTypeEyeState.Mixed)
            {
                g.DrawEllipse(pen, pupilRect);
                g.FillPie(fill, Rectangle.Round(pupilRect), 90, 180);
            }
            else
            {
                g.FillEllipse(fill, pupilRect);
            }

            if (state == StructureTypeEyeState.Hidden)
                g.DrawLine(pen, left + 1f, top + height + 2f, left + width - 1f, top - 2f);

            g.SmoothingMode = oldMode;
        }

        /// <summary>The visible node whose row contains client y, or null.</summary>
        private TreeNode NodeAtRow(int y)
        {
            for (TreeNode node =Tree.TopNode; node is not null; node = node.NextVisibleNode)
            {
                if (node.Bounds.Top > Tree.ClientSize.Height)
                    break;

                if (y >= node.Bounds.Top && y < node.Bounds.Bottom)
                    return node;
            }

            return null;
        }

        /// <summary>Handles a left click in the eye column: toggles the type and its descendants and redraws the viewer.</summary>
        private bool TryToggleEye(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.X < Tree.ClientSize.Width - EyeColumnWidth)
                return false;

            if (NodeAtRow(e.Y)?.Tag is not StructureType type)
                return false;

            HiddenStructureTypeSet.Current.Toggle(type.ID, ChildTypeIds);

            // Every row's eye can change, because the click can reach descendants and a parent's partial state.
            Tree.Invalidate();
            AnnotationOverlay.CurrentOverlay?.InvalidateParent();
            return true;
        }

        private void Tree_MouseDown(object sender, MouseEventArgs e)
        {
            if (TryToggleEye(e))
                return;

            if (e.Button == MouseButtons.Right)
            {
                TreeNode node = Tree.GetNodeAt(e.Location);

                // Only handle empty space clicks - node clicks are handled by ObjectTreeView.OnMouseDown
                if (node is null)
                {
                    Viking.UI.State.SelectedObject = null;
                    ContextMenuStrip menu = new();

                    ToolStripMenuItem menuItem = new("New");
                    menuItem.Click += OnNewStructureType;
                    menu.Items.Add(menuItem);

                    menu.Show(Tree, e.Location);
                }
            }
        }

        private void OnNewStructureType(object sender, EventArgs e)
        {
            StructureTypeObj newTypeObj = new();
            StructureType newType = new(newTypeObj);

            if (newType.ShowPropertiesDialog(ParentForm) == DialogResult.OK)
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
