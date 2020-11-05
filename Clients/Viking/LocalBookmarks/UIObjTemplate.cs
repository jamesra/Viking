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
        protected FolderUIObj _Parent = null;

        /// <summary>
        /// Everyone can use this method to adjust which parent the object has
        /// </summary>
        [Viking.Common.UI.ThisToOneRelationAttribute()]
        public FolderUIObj Parent
        {
            get
            {
                return _Parent;
            }
            set
            {
                if (_Parent != null)
                {
                    _Parent.RemoveChild(this);
                }

                if (value == null)
                    _Parent = Global.FolderUIObjRoot;
                else
                    _Parent = value;

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

        public override string ToString()
        {
            return Name;
        }
        /*
        public override bool Equals(object obj)
        {
            UIObjTemplate<T> objT = obj as UIObjTemplate<T>;
            if (objT == null)
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
        public override System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();

                MenuItem menuProperties = new MenuItem("Properties...");
                menuProperties.Click += OnPropertiesClick;
                menu.MenuItems.Add(menuProperties);

                MenuItem menuDelete = new MenuItem("Delete");
                menuDelete.Click += OnDeleteClick;
                menu.MenuItems.Add(menuDelete);

                return menu;
            }
        }

        public override void Save()
        {
            CallBeforeSave();
            Global.Save();
            CallAfterSave();
        }

        protected virtual void OnPropertiesClick(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this);
        }

        protected virtual void OnDeleteClick(object sender, EventArgs e)
        {
            this.Delete();
        }

        public override Type[] AssignableParentTypes
        {
            get { return new Type[] { typeof(FolderUIObj) }; }
        }

        public override void SetParent(IUIObject parent)
        {
            FolderUIObj parentFolder = parent as FolderUIObj;

            Parent = parentFolder;
        }

    }
}
