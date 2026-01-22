using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI.Controls;
using WebAnnotationModel.Service;

namespace WebAnnotation.Objects
{
    /// <summary>
    /// Base class for all structure objects in the web annotation system
    /// </summary>
#if NET48
    public abstract class StructureBaseObj : IUIObject, IContextMenu
#else
    public abstract class StructureBaseObj : IUIObject
#endif
    {
        #region Variables

        protected long _ID;
        protected string _Name;
        protected string _Description;
        protected long _VolumeID;
        protected long _SectionID;
        protected long _ParentID;
        protected DateTime _Created;
        protected DateTime _Modified;
        protected string _CreatedBy;
        protected string _ModifiedBy;
        protected bool _Visible;
        protected bool _Selected;
        protected Color _Color;
        protected double _Opacity;
        protected string _Tags;

        #endregion

        #region Properties

        public virtual long ID
        {
            get => _ID;
            set
            {
                if (_ID != value)
                {
                    _ID = value;
                    ValueChangedEvent("ID");
                }
            }
        }

        public virtual string Name
        {
            get => _Name;
            set
            {
                if (_Name != value)
                {
                    _Name = value;
                    ValueChangedEvent("Name");
                }
            }
        }

        public virtual string Description
        {
            get => _Description;
            set
            {
                if (_Description != value)
                {
                    _Description = value;
                    ValueChangedEvent("Description");
                }
            }
        }

        public virtual long VolumeID
        {
            get => _VolumeID;
            set
            {
                if (_VolumeID != value)
                {
                    _VolumeID = value;
                    ValueChangedEvent("VolumeID");
                }
            }
        }

        public virtual long SectionID
        {
            get => _SectionID;
            set
            {
                if (_SectionID != value)
                {
                    _SectionID = value;
                    ValueChangedEvent("SectionID");
                }
            }
        }

        public virtual long ParentID
        {
            get => _ParentID;
            set
            {
                if (_ParentID != value)
                {
                    _ParentID = value;
                    ValueChangedEvent("ParentID");
                }
            }
        }

        public virtual DateTime Created
        {
            get => _Created;
            set
            {
                if (_Created != value)
                {
                    _Created = value;
                    ValueChangedEvent("Created");
                }
            }
        }

        public virtual DateTime Modified
        {
            get => _Modified;
            set
            {
                if (_Modified != value)
                {
                    _Modified = value;
                    ValueChangedEvent("Modified");
                }
            }
        }

        public virtual string CreatedBy
        {
            get => _CreatedBy;
            set
            {
                if (_CreatedBy != value)
                {
                    _CreatedBy = value;
                    ValueChangedEvent("CreatedBy");
                }
            }
        }

        public virtual string ModifiedBy
        {
            get => _ModifiedBy;
            set
            {
                if (_ModifiedBy != value)
                {
                    _ModifiedBy = value;
                    ValueChangedEvent("ModifiedBy");
                }
            }
        }

        public virtual bool Visible
        {
            get => _Visible;
            set
            {
                if (_Visible != value)
                {
                    _Visible = value;
                    ValueChangedEvent("Visible");
                }
            }
        }

        public virtual bool Selected
        {
            get => _Selected;
            set
            {
                if (_Selected != value)
                {
                    _Selected = value;
                    ValueChangedEvent("Selected");
                }
            }
        }

        public virtual Color Color
        {
            get => _Color;
            set
            {
                if (_Color != value)
                {
                    _Color = value;
                    ValueChangedEvent("Color");
                }
            }
        }

        public virtual double Opacity
        {
            get => _Opacity;
            set
            {
                if (_Opacity != value)
                {
                    _Opacity = value;
                    ValueChangedEvent("Opacity");
                }
            }
        }

        public virtual string Tags
        {
            get => _Tags;
            set
            {
                if (_Tags != value)
                {
                    _Tags = value;
                    ValueChangedEvent("Tags");
                }
            }
        }

        #endregion

        #region Constructor

        protected StructureBaseObj()
        {
            _ID = -1;
            _Name = "";
            _Description = "";
            _VolumeID = -1;
            _SectionID = -1;
            _ParentID = -1;
            _Created = DateTime.Now;
            _Modified = DateTime.Now;
            _CreatedBy = "";
            _ModifiedBy = "";
            _Visible = true;
            _Selected = false;
            _Color = Color.Black;
            _Opacity = 1.0;
            _Tags = "";
        }

        #endregion

        #region Event Code

        protected event PropertyChangedEventHandler OnValueChanged;
        private event EventHandler OnBeforeDelete;
        private event EventHandler OnAfterDelete;
        private event EventHandler OnBeforeSave;
        private event EventHandler OnAfterSave;
        private event NotifyCollectionChangedEventHandler OnChildChanged;

        protected void ValueChangedEvent(string Column) => OnValueChanged?.Invoke(this, new PropertyChangedEventArgs(Column));

        protected void CallBeforeSave() => OnBeforeSave?.Invoke(this, EventArgs.Empty);

        protected void CallAfterSave() => OnAfterSave?.Invoke(this, EventArgs.Empty);

        protected void CallBeforeDelete() => OnBeforeDelete?.Invoke(this, EventArgs.Empty);

        protected void CallAfterDelete() => OnAfterDelete?.Invoke(this, EventArgs.Empty);

        protected void CallOnChildChanged(NotifyCollectionChangedEventArgs args) => OnChildChanged?.Invoke(this, args);

        #endregion

        #region IUIObject Members

        public event PropertyChangedEventHandler ValueChanged
        {
            add => OnValueChanged += value;
            remove => OnValueChanged -= value;
        }

        public event EventHandler BeforeDelete
        {
            add => OnBeforeDelete += value;
            remove => OnBeforeDelete -= value;
        }

        public event EventHandler AfterDelete
        {
            add => OnAfterDelete += value;
            remove => OnAfterDelete -= value;
        }

        public event EventHandler BeforeSave
        {
            add => OnBeforeSave += value;
            remove => OnBeforeSave -= value;
        }

        public event EventHandler AfterSave
        {
            add => OnAfterSave += value;
            remove => OnAfterSave -= value;
        }

        public virtual void ShowProperties()
        {
            // Default implementation - can be overridden by derived classes
        }

#if NET48
        public virtual ContextMenuStrip ContextMenu => null;
#endif

        public virtual Image SmallThumbnail => null;

        public virtual string ToolTip => Name;

        public virtual void Save()
        {
            // Default implementation - can be overridden by derived classes
        }

        public virtual Type[] AssignableParentTypes => [];

        public virtual void SetParent(IUIObject parent)
        {
            // Default implementation - can be overridden by derived classes
        }

        public virtual GenericTreeNode CreateNode() => new GenericTreeNode(this);

        public virtual int TreeImageIndex => 0;

        public virtual int TreeSelectedImageIndex => 1;

        public event NotifyCollectionChangedEventHandler ChildChanged
        {
            add => OnChildChanged += value;
            remove => OnChildChanged -= value;
        }

        #endregion

        #region Object Overrides

        public override bool Equals(object obj)
        {
            if (obj is StructureBaseObj other)
            {
                return this.ID == other.ID && this.VolumeID == other.VolumeID;
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + ID.GetHashCode();
                hash = hash * 23 + VolumeID.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => string.IsNullOrEmpty(_Name) ? GetType().Name : _Name;

        #endregion
    }
}
