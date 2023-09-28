using AnnotationService.Types;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace WebAnnotationModel.Objects
{
    abstract public class WCFObjBaseWithParent<KEY, T, THISTYPE> : WCFObjBaseWithKey<KEY, T>, System.Collections.Specialized.INotifyCollectionChanged
        where KEY : struct, IEquatable<KEY>
        where T : AnnotationService.Types.DataObjectWithParentOfLong, new()
        where THISTYPE : WCFObjBaseWithParent<KEY, T, THISTYPE>, new()
    {
        /// <summary>
        /// This method is called when the Parent property has been requested, the ParentID exists, but the parent object has not been set
        /// Returns the object representing the missing parent
        /// </summary>
        protected abstract THISTYPE OnMissingParent();

        private THISTYPE _Parent = null;
        public abstract KEY? ParentID { get; set; }

        public THISTYPE Parent
        {
            get
            {
                if (_Parent == null && ParentID.HasValue)
                    _Parent = OnMissingParent();

                return _Parent;
            }
            set
            {
                //Do nothing if the parent isn't changed
                if (_Parent == value)
                {
                    if (_Parent == null)
                        return;

                    //When replacing a new object created in the database we can have deleted ourselves from the parent but need to add ourselves again.
                    if (_Parent.Children.Contains(this) == false)
                    {
                        _Parent.AddChild(this as THISTYPE);

                    }
                    return;
                }

                //Make sure we aren't being assigned to a parent who hasn't been saved to the database yet, because we don't know how to 
                //write that insert statement
                if (value != null)
                {
                    Debug.Assert(value.DBAction != DBACTION.INSERT);
                    if (value.DBAction == DBACTION.INSERT)
                        throw new ArgumentException("Cannot set StructureType.Parent to a parent who is not yet in the database.");
                }

                OnPropertyChanging("Parent");

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
                    if (ParentID == null)
                        SetUpdateFlag = true;
                    else if (!_Parent.ID.Equals(ParentID.Value))
                        SetUpdateFlag = true;

                    this.ParentID = new KEY?(value.ID);
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

                OnPropertyChanged("Parent");
            }
        }

        readonly ObservableCollection<THISTYPE> _Children = new ObservableCollection<THISTYPE>();

        public THISTYPE[] Children
        {
            get { return _Children.ToArray(); }
        }

        protected void AddChild(THISTYPE child)
        {
            Debug.Assert(_Children.Contains(child) == false);
            if (_Children.Contains(child) == false)
                _Children.Add(child);
            else
            {
                //Update the array with the new child?
                int iChild = _Children.IndexOf(child);
                _Children[iChild] = child;
            }//            SetDBActionForChange(); Don't do this, the database doesn't care if the child changes, tables only carry a parent field
        }

        //Used by store to remove a child without changing the childs parentID.  This can be important after creating a temporary ID and then updating the ID after writing to the database.
        internal void RemoveChild(THISTYPE child)
        {
            Debug.Assert(_Children.Contains(child));
            if (_Children.Contains(child))
                _Children.Remove(child);
            //            SetDBActionForChange(); Don't do this, the database doesn't care if the child changes, tables only carry a parent field
        }

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler ChildChanged
        {
            add { _Children.CollectionChanged += value; }
            remove { _Children.CollectionChanged -= value; }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add
            {
                ((INotifyCollectionChanged)_Children).CollectionChanged += value;
            }

            remove
            {
                ((INotifyCollectionChanged)_Children).CollectionChanged -= value;
            }
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
                    this._Parent = null;
                }
            }

            base.Synch(newdata);
        }
    }
}
