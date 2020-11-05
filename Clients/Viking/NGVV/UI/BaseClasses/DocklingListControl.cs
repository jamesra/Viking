using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI.BaseClasses
{
    public partial class DockingListControl : Viking.UI.BaseClasses.DockableUserControl
    {
        public object SelectedObject = null;

        #region Colors

        [Browsable(true)]
        public System.Drawing.Color ListForeColor
        {
            get
            {
                return this.ListItems.ForeColor;
            }
            set
            {
                this.ListItems.ForeColor = value;
            }
        }

        [Browsable(true)]
        public System.Drawing.Color ListBackColor
        {
            get
            {
                return this.ListItems.BackColor;
            }
            set
            {
                this.ListItems.BackColor = value;
            }
        }

        [Browsable(true)]
        public System.Drawing.Color TitleForeColor
        {
            get
            {
                return this.LabelTitle.ForeColor;
            }
            set
            {
                this.LabelTitle.ForeColor = value;
            }
        }

        [Browsable(true)]
        public System.Drawing.Color TitleBackColor
        {
            get
            {
                return this.LabelTitle.BackColor;
            }
            set
            {
                this.LabelTitle.BackColor = value;
            }
        }

        #endregion

        public DockingListControl()
        {
            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();

            this.ListItems.Font = Global.Default.Font;
            this.ListItems.ForeColor = Global.Default.ForeColor;
            this.ListItems.BackColor = Global.Default.BackColor;

            // create our handler for when the parent form is closing
            OnParentFormClosing = new CancelEventHandler(parentForm_Closing);
        }

        private void DockingList_Resize(object sender, System.EventArgs e)
        {
            Size NewSize = this.Size;
            NewSize.Height -= this.LabelTitle.Height;
            ListItems.Size = NewSize;
        }

        private void ListItems_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            ListView List = this.ListItems;

            if (List.SelectedItems.Count > 0)
            {
                ListViewItem SelectedItem = List.SelectedItems[0];
                IUIObject Obj = SelectedItem.Tag as IUIObject;
                if (Obj != null)
                {
                    Viking.UI.State.SelectedObject = Obj;
                    this.SelectedObject = Obj;
                }
            }
        }

        public ListViewItem FindItem(IUIObject Obj)
        {
            if (Obj == null)
                return null;

            foreach (ListViewItem Item in ListItems.Items)
            {
                IUIObject ItemObj = Item.Tag as IUIObject;
                if (ItemObj != null && Obj == ItemObj)
                    return Item;
            }

            return null;
        }

        public void ClearItems()
        {
            this.ListItems.BeginUpdate();

            for (int i = 0; i < ListItems.Items.Count; i++)
                RemoveEvents(ListItems.Items[i] as IUIObject);

            this.ListItems.Clear();

            this.ListItems.EndUpdate();
        }

        private void RemoveEvents(IUIObject Obj)
        {
            if (Obj != null)
            {
                Obj.ValueChanged -= this.OnValueChangeEventHandler;
                Obj.BeforeDelete -= this.BeforeDeleteEventHandler;
                Obj.AfterDelete -= this.OnDeleteEventHandler;
            }
        }

        public void RemoveItem(ListViewItem Item)
        {
            IUIObject Obj = Item.Tag as IUIObject;
            RemoveEvents(Obj);
            ListItems.Items.Remove(Item);
        }

        /// <summary>
        /// Displays a standard DBObject context menu if the users mouse is over an item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListItems_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ListViewItem Item = ListItems.GetItemAt(e.X, e.Y);
                IUIObject ContextObj = null;
                if (Item == null)
                    ContextObj = this.DefaultContextMenuObject;
                else
                    ContextObj = Item.Tag as IUIObject;

                //If we clicked an item on the list, show the context menu for the item.
                //Otherwise, show the generic context menu for the item type the list shows. 
                if (ContextObj != null)
                {
                    //	ListItems.ContextMenu = ContextObj.ContextMenu;
                    //ContextMenu NewMenu = SharedContextMenu.BuildMenuFor(ContextObj);
                    //ListItems.ContextMenu = NewMenu; 
                }
                else
                {
                    //					SupportedUITypesAttribute[] ListTypes = this.GetType().GetCustomAttributes(typeof(SupportedUITypesAttribute), true) as SupportedUITypesAttribute[]; 
                    //					if(ListTypes != null && ListTypes.Length > 0)
                    //					{
                    //                        SupportedUITypesAttribute ListType = ListTypes[0]; 

                    //ListItems.ContextMenu = SharedContextMenu.BuildMenuFor(ListType.ListType); 						
                    //					}
                }
            }
        }

        public void DisplayObjects(IUIObject[] Objects)
        {
            ListItems.DisplayObjects(Objects);
        }

        private void ListItems_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListViewItem item = this.ListItems.GetItemAt(e.X, e.Y);
            if (item == null)
                return;

            IUIObject obj = item.Tag as IUIObject;
            if (obj == null)
                return;

            OnObjectDoubleClick(obj);

        }

        private CancelEventHandler OnParentFormClosing = null;
        private Form _ParentForm = null;

        protected virtual void parentForm_Closing(object sender, CancelEventArgs e)
        {
            this.ClearItems();
        }

        protected override void OnParentBindingContextChanged(EventArgs e)
        {
            // do we have a parent?
            if (this.Parent == null)
                return;

            // walk up parents looking for a form
            Control currentControl = this;
            while (currentControl != null && (currentControl as Form) == null)
                currentControl = currentControl.Parent;

            // did we find a form?
            if (currentControl != null)
            {
                // is our parent different?
                if (_ParentForm != currentControl)
                {
                    // do we have an old binding to remove
                    if (_ParentForm != null)
                        _ParentForm.Closing -= OnParentFormClosing;

                    // add our new binding 
                    (currentControl as Form).Closing += OnParentFormClosing;
                }
            }
            else
            {
                Trace.WriteLine("DataObjectListView.OnParentBindingContextChanged: couldn't find parent form to bind to", "UI");
            }

            base.OnParentBindingContextChanged(e);
        }

        protected virtual void OnObjectDoubleClick(IUIObject obj)
        {

        }
    }
}
