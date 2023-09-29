using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI.BaseClasses
{
    public partial class DockingTreeControl : Viking.UI.BaseClasses.DockableUserControl
    {
        protected TreeNode ContextMenuNode = null;

        public DockingTreeControl()
        {
            InitializeComponent();

            SetDragDropTypes();
        }

        public void SetDragDropTypes()
        {
            if (!(this.GetType().GetCustomAttributes(typeof(SupportedUITypesAttribute), true) is SupportedUITypesAttribute[] attribs))
                return;

            //Should only be one entry, but lets be safe and add them together
            List<Type> supportedTypes = new List<Type>();
            foreach (SupportedUITypesAttribute attrib in attribs)
            {
                supportedTypes.AddRange(attrib.Types);
            }

            Tree.ValidDragDropTypes = supportedTypes.ToArray();
        }

        #region Properties

        [Browsable(false)]
        public IUIObject SelectedObject
        {
            get
            {
                return Tree.SelectedObject;
            }
            set
            {
                Tree.SelectedObject = value;
            }
        }

        #endregion

        private void DockingTreeControl_Load(object sender, EventArgs e)
        {
            Tree.BeginUpdate();
            this.InitializeTree();
            Tree.EndUpdate();
        }

        protected virtual void InitializeTree() { }

    }
}
