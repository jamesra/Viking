using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.Common;
using System.Windows.Forms;
using WebAnnotation.Service; 

namespace WebAnnotation.Objects
{
    /// <summary>
    /// This class represents a link between locations. This object is a little unique because it is
    /// not tied to the database object like the other *obj classes
    /// </summary>
    public class LocationLinkObj : IUIObject
    {
        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public LocationObj LocationOnSection;

        /// <summary>
        /// LocationOnSection is the location on the reference section
        /// </summary>
        public long LocationOnReference;

        public RoundLineCode.RoundLine lineGraphic;
        public Geometry.GridLineSegment lineSegment;

        public double Radius
        {
            get
            {
                return LocationOnSection.Radius / 4.0f; 
            }
        }

        public LocationLinkObj(LocationObj LocOnSection, long LocOnRef, Geometry.GridLineSegment line)
        {
            this.LocationOnSection = LocOnSection; 
            this.LocationOnReference = LocOnRef;
            lineSegment = line;
            lineGraphic = new RoundLineCode.RoundLine((float)line.A.X,
                                                      (float)line.A.Y,
                                                      (float)line.B.X,
                                                      (float)line.B.Y);
        }

        #region IUIObjectBasic Members

        void IUIObjectBasic.ShowProperties()
        {
            throw new NotImplementedException();
        }

        System.Windows.Forms.ContextMenu IUIObjectBasic.ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();

                MenuItem menuSeperator = new MenuItem(); 
                MenuItem menuDelete = new MenuItem("Delete", ContextMenu_OnDelete);

                menu.MenuItems.Add(menuSeperator); 
                menu.MenuItems.Add(menuDelete); 

                return menu; 
            }
        }

        string IUIObjectBasic.ToolTip
        {
            get { throw new NotImplementedException(); }
        }

        void IUIObjectBasic.Save()
        {
            throw new NotImplementedException();
        }

        #endregion

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public void Delete()
        {
            CallBeforeDelete(); 

            Store.Locations.DeleteLink(this.LocationOnSection.ID, this.LocationOnReference);

            CallAfterDelete();
        }

        #region Events

        public static event EventHandler OnCreate;
        private event ValueChangedEventHandler OnValueChanged;
        internal event EventHandler OnBeforeDelete;
        internal event EventHandler OnAfterDelete;
        internal event EventHandler OnBeforeSave;
        internal event EventHandler OnAfterSave;
        private event ChildChangedEventHandler OnChildChanged;

        protected void ValueChangedEvent(string Column)
        {
            if (OnValueChanged != null)
            {
                //We need to ensure these events are invoked on the main thread since UI controls listen to them and they can only 
                //change state on the main thread 
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnValueChanged, new object[] { this, new ValueChangedEventArgs(Column) });
            }
        }

        internal void CallOnCreate()
        {
            if (OnCreate != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, new object[] { this, null });
            }
        }

        internal void CallBeforeSave()
        {
            if (OnBeforeSave != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnBeforeSave, new object[] { this, null });
            }
        }

        internal void CallAfterSave()
        {
            if (OnAfterSave != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnAfterSave, new object[] { this, null });
            }
        }

        internal void CallBeforeDelete()
        {
            if (OnBeforeDelete != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnBeforeDelete, new object[] { this, null });
            }
        }

        internal void CallAfterDelete()
        {
            if (OnAfterDelete != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnAfterDelete, new object[] { this, null });
            }
        }

        internal void CallOnChildChanged(ChildChangeEventArgs args)
        {
            if (OnChildChanged != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnChildChanged, new object[] { this, args });
            }
        }

        #endregion

        #region IUIObject Members

        public event ValueChangedEventHandler ValueChanged
        {
            add { OnValueChanged += value; }
            remove { OnValueChanged -= value; }
        }

        public event EventHandler BeforeDelete
        {
            add { OnBeforeDelete += value; }
            remove { OnBeforeDelete -= value; }
        }

        public event EventHandler AfterDelete
        {
            add { OnAfterDelete += value; }
            remove { OnAfterDelete -= value; }
        }

        public event EventHandler BeforeSave
        {
            add { OnBeforeSave += value; }
            remove { OnBeforeSave -= value; }
        }

        event EventHandler IUIObject.AfterSave
        {
            add { OnAfterSave += value; }
            remove { OnAfterSave -= value; }
        }

        public event ChildChangedEventHandler ChildChanged
        {
            add { OnChildChanged += value; }
            remove { OnChildChanged -= value; }
        }

        System.Drawing.Image IUIObject.SmallThumbnail
        {
            get { throw new NotImplementedException(); }
        }

        Type[] IUIObject.AssignableParentTypes
        {
            get { throw new NotImplementedException(); }
        }

        void IUIObject.SetParent(IUIObject parent)
        {
            throw new NotImplementedException();
        }

        Viking.UI.Controls.GenericTreeNode IUIObject.CreateNode()
        {
            throw new NotImplementedException();
        }

        int IUIObject.TreeImageIndex
        {
            get { throw new NotImplementedException(); }
        }

        int IUIObject.TreeSelectedImageIndex
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

    }
}
