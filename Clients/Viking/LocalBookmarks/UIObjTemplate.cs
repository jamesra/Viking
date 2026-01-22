using System;
using System.Windows.Forms;
using Viking.Common;

namespace LocalBookmarks
{
    abstract class UIObjTemplate<T> : Viking.Objects.UIObjBase
    {
        /// <summary>
        /// Set this parameter if we are loading the data and don't need to create the child in the store
        /// </summary>
        protected FolderUIObj? _Parent = null;

        /// <summary>
        /// Everyone can use this method to adjust which parent the object has
        /// </summary>
        [Viking.Common.UI.ThisToOneRelationAttribute()]
        public FolderUIObj Parent
        {
            get => _Parent;
            set
            {
                _Parent?.RemoveChild(this);

                _Parent = value is null ? Global.FolderUIObjRoot : value;

                _Parent.AddChild(this);
            }
        }

        public abstract string Name
        {
            get;
            set;
        }

        //The class holding data for underlying store
        public T Data;

        public string FullPathString()
        {
            if (Parent != null)
                return Parent.ToString() + System.IO.Path.DirectorySeparatorChar + Name;
            else
                return Name;
        }

        public override string ToString() => Name;
        /*
        public override bool Equals(object obj)
        {
            UIObjTemplate<T> objT = obj as UIObjTemplate<T>;
            if (objT is null)
                return false;

            return objT.FullPathString() == this.FullPathString();
        }

        private int? _HashCode;
        public override int GetHashCode()
        {
            if (_HashCode.HasValue)
                return _HashCode.Value;

            _HashCode = new int?(this.FullPathString().GetHashCode());
            return _HashCode.Value; 
        }
        */
        public override System.Windows.Forms.ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip menu = new();

                ToolStripMenuItem menuProperties = new("Properties...");
                menuProperties.Click += OnPropertiesClick;
                menu.Items.Add(menuProperties);

                ToolStripMenuItem menuDelete = new("Delete");
                menuDelete.Click += OnDeleteClick;
                menu.Items.Add(menuDelete);

                return menu;
            }
        }

        public override void Save()
        {
            CallBeforeSave();
            Global.Save();
            CallAfterSave();
        }

        protected virtual void OnPropertiesClick(object sender, EventArgs e) => Viking.UI.Forms.PropertySheetForm.Show(this);

        protected virtual void OnDeleteClick(object sender, EventArgs e) => this.Delete();

        public override Type[] AssignableParentTypes => [typeof(FolderUIObj)];

        public override void SetParent(IUIObject parent)
        {
            if (parent is FolderUIObj parentFolder)
                Parent = parentFolder;
        }

    }
}
