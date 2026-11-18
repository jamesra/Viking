using AnnotationService.Types;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace WebAnnotationModel.Objects
{
    public abstract class WCFObjBaseWithParent<KEY, T, THISTYPE> : WCFObjBaseWithKey<KEY, T>, System.Collections.Specialized.INotifyCollectionChanged
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

        /// <summary>
        /// Drops the cached parent so the next <see cref="Parent"/> get reloads it for the current <see cref="ParentID"/>.
        /// Used by <c>Update</c> overrides that change ParentID without going through the Parent setter.
        /// </summary>
        protected void ClearCachedParent() => _Parent = null;

        public abstract KEY? ParentID { get; set; }

        public THISTYPE Parent
        {
            get
            {
                if (!ParentID.HasValue)
                    return null;

                if (_Parent is null || !_Parent.ID.Equals(ParentID.Value))
                    _Parent = OnMissingParent();

                return _Parent;
            }
            set
            {
                //Do nothing if the parent isn't changed
                if (_Parent == value)
                {
                    if (_Parent is null)
                        return;

                    //When replacing a new object created in the database we can have deleted ourselves from the parent but need to add ourselves again.
                    if (!_Parent.HasChild(this as THISTYPE))
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
                _Parent?.RemoveChild(this as THISTYPE);

                _Parent = value;

                //Need to update the underlying type so we persist the change if asked
                bool SetUpdateFlag = false;
                if (_Parent != null)
                {
                    if (ParentID is null)
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

        /// <summary>
        /// The loaded children. Region queries complete on WCF callback threads and set <see cref="Parent"/> concurrently,
        /// so every read and write holds <see cref="childrenLock"/>; a plain list raced there and threw from
        /// <c>Contains</c>. Change events are raised after the lock is released, since handlers update UI (tree nodes).
        /// </summary>
        private readonly System.Collections.Generic.List<THISTYPE> _Children = [];

        private readonly object childrenLock = new();

        private NotifyCollectionChangedEventHandler childrenChanged;

        /// <summary>
        /// A snapshot of the children that are loaded in memory. Never null and never contains null; children that
        /// exist only on the server are not listed, and reading this never contacts the server.
        /// </summary>
        public THISTYPE[] Children
        {
            get
            {
                lock (childrenLock)
                    return _Children.ToArray();
            }
        }

        internal bool HasChild(THISTYPE child)
        {
            lock (childrenLock)
                return _Children.Contains(child);
        }

        protected void AddChild(THISTYPE child)
        {
            NotifyCollectionChangedEventArgs args;
            lock (childrenLock)
            {
                int iChild = _Children.IndexOf(child);
                if (iChild < 0)
                {
                    _Children.Add(child);
                    args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, child, _Children.Count - 1);
                }
                else
                {
                    //Update the array with the new child
                    THISTYPE old = _Children[iChild];
                    _Children[iChild] = child;
                    args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, child, old, iChild);
                }
            }

            // SetDBActionForChange(); Don't do this, the database doesn't care if the child changes, tables only carry a parent field
            childrenChanged?.Invoke(this, args);
        }

        //Used by store to remove a child without changing the childs parentID.  This can be important after creating a temporary ID and then updating the ID after writing to the database.
        internal void RemoveChild(THISTYPE child)
        {
            NotifyCollectionChangedEventArgs args;
            lock (childrenLock)
            {
                int iChild = _Children.IndexOf(child);
                if (iChild < 0)
                    return;

                _Children.RemoveAt(iChild);
                args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, child, iChild);
            }

            childrenChanged?.Invoke(this, args);
        }

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler ChildChanged
        {
            add => childrenChanged += value;
            remove => childrenChanged -= value;
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => childrenChanged += value;
            remove => childrenChanged -= value;
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

        internal override void Update(T newdata)
        {
            if (this.Data != null && this.Data.ParentID != newdata.ParentID)
                this._Parent = null;

            base.Update(newdata);
        }
    }
}
