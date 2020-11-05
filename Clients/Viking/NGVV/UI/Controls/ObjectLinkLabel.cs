using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI.Controls
{
    public partial class ObjectLinkLabel : UserControl
    {
        [Browsable(true)]
        [Category("Data")]
        public new string Text
        {
            get
            {
                return this.txtName.Text;
            }
            set
            {
                this.txtName.Text = value;
                base.Text = value;
            }
        }

        private IUIObject _SourceObject;
        private Type _Type;

        public ObjectLinkLabel()
        {
            InitializeComponent();
        }

        private void ObjectLinkLabel_Load(object sender, EventArgs e)
        {

        }

        public IUIObject SourceObject
        {
            get
            {
                return _SourceObject;
            }
            set
            {
                _SourceObject = value;
                if (null != _SourceObject)
                    SourceType = _SourceObject.GetType();

                if (_SourceObject == null)
                {
                    this.Text = "";
                    this.Pict.Visible = false;
                }
                else
                {
                    this.Text = _SourceObject.ToString();
                    this.Pict.Image = _SourceObject.SmallThumbnail;
                    this.Pict.Visible = true;
                }
            }
        }

        public Type SourceType
        {
            get
            {
                return _Type;
            }
            set
            {
                _Type = value;

                EnableControls();
            }
        }

        /// <summary>
        /// can the object in the control be edited
        /// </summary>
        private bool _ReadOnly = false;

        public bool ReadOnly
        {
            get { return _ReadOnly; }
            set
            {
                _ReadOnly = value;
                EnableControls();
            }
        }

        public override ContextMenu ContextMenu
        {
            get
            {
                if (_SourceObject == null)
                    return base.ContextMenu;
                ContextMenu CMenu = ((IUIObject)_SourceObject).ContextMenu;
                CMenu.MenuItems.Add("Clear Link", new EventHandler(ContextMenuOnClear));
                return CMenu;
            }
            set
            {
                System.Diagnostics.Debug.Assert(false, "No implemented");
                base.ContextMenu = value;
            }
        }

        private void ContextMenuOnClear(object sender, EventArgs e)
        {
            SourceObject = null;
        }

        protected void EnableControls()
        {
            if (_Type == null || _ReadOnly)
            {
                btnBrowse.Visible = false;
                this.AllowDrop = false;
            }
            else
            {
                btnBrowse.Visible = true;
                this.AllowDrop = true;
            }
        }


        protected override void OnDragDrop(System.Windows.Forms.DragEventArgs e)
        {
            Debug.Assert(_Type != null);
            if (_Type.IsAssignableFrom(UI.State.DragDropObject.GetType()))
                SourceObject = UI.State.DragDropObject as IUIObject;
            base.OnDragDrop(e);
        }

        protected override void OnDragOver(System.Windows.Forms.DragEventArgs e)
        {
            if (_Type == null)
                e.Effect = DragDropEffects.None;
            else if (_ReadOnly == false && _Type.IsAssignableFrom(UI.State.DragDropObject.GetType()))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;

            base.OnDragOver(e);
        }

        private void txtName_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_SourceObject != null)
            {
                if (e.Button == MouseButtons.Right)
                    this.ContextMenu.Show(this, new Point(e.X, e.Y));
                else
                    ((IUIObject)_SourceObject).ShowProperties();
            }
        }

        private void btnBrowse_Click(object sender, System.EventArgs e)
        {
            /* TODO: Add ChooseObjectForm 
            ChooseObjectForm form = new ChooseObjectForm();
            form.SearchType = this._Type;
            if (form.ShowDialog() == DialogResult.OK)
            {
                this.SourceObject = form.SelectedObject;
            }
             */
        }
    }
}
