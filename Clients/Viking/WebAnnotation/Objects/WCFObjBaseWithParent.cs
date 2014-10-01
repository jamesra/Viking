using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.Common;
using System.Diagnostics;
using WebAnnotation.Service;

namespace WebAnnotation.Objects
{
    abstract public class WCFObjBaseWithParent<T, THISTYPE> : WCFObjBaseWithKey<T>
        where T : DataObjectWithParentOflong, new()
        where THISTYPE : WCFObjBaseWithParent<T, THISTYPE>, new()
    {

        /// <summary>
        /// This method is called when the Parent property has been requested, the ParentID exists, but the parent object has not been set
        /// Returns the object representing the missing parent
        /// </summary>
        protected abstract THISTYPE OnMissingParent();

        private THISTYPE _Parent = null;
        public long? ParentID
        {
            get { return Data.ParentID; }
        }

        public THISTYPE Parent
        {
            get
            {
                if(_Parent == null && ParentID.HasValue)
                    _Parent = OnMissingParent();

                return _Parent; 
            }
            set
            {
                //Do nothing if the parent isn't changed
                if (_Parent == value)
                    return;

                //Make sure we aren't being assigned to a parent who hasn't been saved to the database yet, because we don't know how to 
                //write that insert statement
                if (value != null)
                {
                    Debug.Assert(value.DBAction != DBACTION.INSERT);
                    if (value.DBAction == DBACTION.INSERT)
                        throw new ArgumentException("Cannot set StructureType.Parent to a parent who is not yet in the database.");
                }

                //Remove ourselves from our old parent's list of children
                if (_Parent != null)
                {
                    _Parent.RemoveChild(this as THISTYPE);
                }

                _Parent = value;

                //Need to update the underlying type so we persist the change if asked
                bool SetUpdateFlag = false; 
                if (_Parent != null)
                {
                    if (_Parent.ID != Data.ParentID)
                        SetUpdateFlag = true; 
                        
                    Data.ParentID = new long?(value.ID);
                    _Parent.AddChild(this as THISTYPE);
                }
                else
                {
                    if (Data.ParentID.HasValue)
                        SetUpdateFlag = true; 

                    Data.ParentID = new long?();
                }

                if (SetUpdateFlag)
                    DBAction = DBACTION.UPDATE; 

                ValueChangedEvent("Parent");
            }
        }

        public override void SetParent(IUIObject parent)
        {
            THISTYPE p = parent as THISTYPE;
            if (p != null)
                this.Parent = p;
        }

        List<THISTYPE> _Children = new List<THISTYPE>();
        [Common.UI.ThisToManyRelation]
        public THISTYPE[] Children
        {
            get { return _Children.ToArray(); }
        }

        protected void AddChild(THISTYPE child)
        {
            _Children.Add(child);
            //            SetDBActionForChange(); Don't do this, the database doesn't care if the child changes, tables only carry a parent field
            CallOnChildChanged(new ChildChangeEventArgs(child, CHANGEACTION.ADD));
        }

        protected void RemoveChild(THISTYPE child)
        {
            _Children.Remove(child);
            //            SetDBActionForChange(); Don't do this, the database doesn't care if the child changes, tables only carry a parent field
            CallOnChildChanged(new ChildChangeEventArgs(child, CHANGEACTION.REMOVE));
        }

        /// <summary>
        /// Called when the database is queried and the results might have new values for our object
        /// </summary>
        /// <param name="newdata"></param>
        internal override void Synch(T newdata)
        {
            if (this.Data != null)
            {
                if (this.Data.ParentID != newdata.ParentID)
                {
                    this.Parent = null;
                }
            }

            base.Synch(newdata);
        }

    }
}
