using System;
using System.ComponentModel; 
using System.Collections.Generic;
using System.Collections.Specialized; 
using System.Linq;
using System.Text;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; 
using Viking.Common;
using WebAnnotation;
using WebAnnotationModel;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using Common.UI;
using WebAnnotation.UI.Commands;
using System.Collections.Concurrent;

namespace WebAnnotation.ViewModel
{
    public class Location_ViewModelBase : Viking.Objects.UIObjBase, IEqualityComparer<Location_ViewModelBase>, IEqualityComparer<LocationObj>, IComparable<Location_ViewModelBase>, System.Windows.IWeakEventListener
    {
        public readonly LocationObj modelObj;

        public Location_ViewModelBase(LocationObj location)
        {
            Debug.Assert(location != null);

            this.modelObj = location;
        }

        [Column("ID")]
        public long ID
        {
            get { return modelObj.ID; }
        }

        public override string ToString()
        {
            return modelObj.ToString();
        }

        public override int GetHashCode()
        {
            return modelObj.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            Location_ViewModelBase LocObj = obj as Location_ViewModelBase;
            if (LocObj != null)
            {
                return modelObj.Equals(LocObj.modelObj);
            }

            LocationObj LocObj2 = obj as LocationObj;
            if (LocObj2 != null)
            {
                return modelObj.Equals(LocObj2);
            }

            return false;
        }

        public static bool operator ==(Location_ViewModelBase A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(Location_ViewModelBase A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if ((object)A != null)
                return !A.Equals(B);

            return true;
        }

        public string Label
        {
            get
            {
                if (Parent == null)
                    return "";

                if (Parent.Type == null)
                    return "";

                return Parent.Type.Code + " " + Parent.ID.ToString();
            }
        }

        public long? ParentID
        {
            get { return modelObj.ParentID; }
        }

        private Structure _Parent = null;

        private void ResetParentCache() { _Parent = null; }

        public Structure Parent
        {
            get
            {
                if (this.modelObj.Parent == null)
                    return null;

                if (this._Parent == null)
                    _Parent = new Structure(this.modelObj.Parent);

                return _Parent;
            }
        }

        #region Weak Events
        private object EventsLock = new object();
        private bool EventsRegistered = false;
        internal void RegisterForLocationEvents()
        {
            if (EventsRegistered)
                return;

            lock (EventsLock)
            {
                if (EventsRegistered)
                    return;

                NotifyPropertyChangedEventManager.AddListener(this.modelObj, this);

                if (this.modelObj.Parent == null)
                {
                    Action<long> GetParent = delegate(long ParentID)
                    {
                        StructureObj parent = Store.Structures.GetObjectByID(ParentID, true);
                        if (parent != null)
                            NotifyPropertyChangedEventManager.AddListener(this.modelObj.Parent, this);
                    };

                    AnnotationOverlay.CurrentOverlay.Parent.BeginInvoke(GetParent, new object[] { this.modelObj.ParentID.Value });
                }
                else
                    NotifyPropertyChangedEventManager.AddListener(this.modelObj.Parent, this);

                EventsRegistered = true;
            }
        }

        internal void DeregisterForLocationEvents()
        {
            if (!EventsRegistered)
                return;

            lock (EventsLock)
            {
                if (!EventsRegistered)
                    return;

                NotifyPropertyChangedEventManager.RemoveListener(this.modelObj, this);
                NotifyPropertyChangedEventManager.RemoveListener(this.modelObj.Parent, this);

                EventsRegistered = false;
            }
        }
        #endregion

        #region IUIObject Members

        public override void Delete()
        {
            Store.Locations.Remove(this.modelObj);
            Store.Locations.Save();

            if (this.ParentID.HasValue)
                Store.Structures.CheckForOrphan(this.ParentID.Value);
        }

        public new event PropertyChangedEventHandler ValueChanged
        {
            add { modelObj.PropertyChanged += value; }
            remove { modelObj.PropertyChanged -= value; }
        }

        protected ContextMenu _AddTerminalOffEdgeMenus(ContextMenu menu)
        {
            MenuItem menuExtensible = new MenuItem("Terminal", ContextMenu_OnTerminal);
            MenuItem menuOffEdge = new MenuItem("Off Edge", ContextMenu_OnOffEdge);

            menuExtensible.Checked = this.modelObj.Terminal;
            menuOffEdge.Checked = this.modelObj.OffEdge;

            menu.MenuItems.Add(menuExtensible);
            menu.MenuItems.Add(menuOffEdge);

            return menu;
        }

        protected ContextMenu _AddDeleteMenu(ContextMenu menu)
        {
            MenuItem menuSeperator = new MenuItem();
            MenuItem menuDelete = new MenuItem("Delete", ContextMenu_OnDelete);

            menu.MenuItems.Add(menuSeperator);
            menu.MenuItems.Add(menuDelete);

            return menu;
        }

        public override ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();
                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                this._AddTerminalOffEdgeMenus(menu);
                this._AddDeleteMenu(menu);

                return menu;
            }
        }

        public override Image SmallThumbnail
        {
            get { throw new NotImplementedException(); }
        }

        public override string ToolTip
        {
            get { throw new NotImplementedException(); }
        }

        public override void Save()
        {
            Store.Locations.Save();
        }

        #endregion


        protected void ContextMenu_OnProperties(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this.Parent);
        }

        protected void ContextMenu_OnTerminal(object sender, EventArgs e)
        {
            this.modelObj.Terminal = !this.modelObj.Terminal;
            bool success = Store.Locations.Save();
            if (!success)
            {
                this.modelObj.Terminal = !this.modelObj.Terminal;
            }

        }

        protected void ContextMenu_OnOffEdge(object sender, EventArgs e)
        {
            /*
            DBACTION originalDBAction = this.DBAction;
            this.Data.OffEdge = !this.Data.OffEdge;
            this.Data.DBAction = DBACTION.UPDATE;
            bool success = Store.Locations.Save();
            if (!success)
            {
                this.Data.OffEdge = !this.Data.OffEdge;
                this.DBAction = originalDBAction;
            }
             */
            this.modelObj.OffEdge = !this.modelObj.OffEdge;
            Store.Locations.Save();
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }


        public bool Equals(Location_ViewModelBase x, Location_ViewModelBase y)
        {
            if (x == null && y == null)
                return true;

            if (x == null || y == null)
                return false;

            return x.ID == y.ID;
        }

        public int GetHashCode(Location_ViewModelBase obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj", "GetHashCode");

            return obj.modelObj.GetHashCode();
        }

        public bool Equals(LocationObj x, LocationObj y)
        {
            if (x == null && y == null)
                return true;

            if (x == null || y == null)
                return false;

            return x.ID == y.ID;
        }

        public int GetHashCode(LocationObj obj)
        {
            return obj.GetHashCode();
        }

        int IComparable<Location_ViewModelBase>.CompareTo(Location_ViewModelBase other)
        {
            if (other == null)
                return 1;

            return (int)(this.ID - other.ID);
        }

        #region WeakEvents

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            PropertyChangedEventArgs PropertyChangedArgs = e as PropertyChangedEventArgs;
            if (PropertyChangedArgs != null)
            {
                StructureObj structObj = sender as StructureObj;
                if (structObj != null && structObj.ID == this.modelObj.ParentID)
                    this.OnParentPropertyChanged(sender, PropertyChangedArgs);
                else
                {
                    this.OnObjPropertyChanged(sender, PropertyChangedArgs);
                }

                return true;
            }

            System.Collections.Specialized.NotifyCollectionChangedEventArgs CollectionChangeArgs = e as System.Collections.Specialized.NotifyCollectionChangedEventArgs;
            if (CollectionChangeArgs != null)
            {
                this.OnLinksChanged(sender, CollectionChangeArgs);
                return true;
            }

            Debug.Fail("Weak Event not handled");
            return false;
        }

        protected virtual void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            this.ResetParentCache();
            return;
        }

        protected virtual void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            return;
        }

        protected virtual void OnLinksChanged(object o, NotifyCollectionChangedEventArgs args)
        {
            return;
        }
        #endregion


        #region Properties



        [Column("X")]
        public double X
        {
            get { return modelObj.Position.X; }
        }

        [Column("Y")]
        public double Y
        {
            get { return modelObj.Position.Y; }
        }

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        [Column("Z")]
        public double Z
        {
            get { return modelObj.Z; }
        }

        [Column("Last Editor")]
        public string Username
        {
            get { return modelObj.Username; }
        }

        [Column("Modified")]
        public DateTime LastModified
        {
            get { return modelObj.LastModified; }
        }

        /// <summary>
        /// VolumeX is the x position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        /// 
        [Column("VolumeX")]
        public double VolumeX
        {
            get
            {
                return modelObj.VolumePosition.X;
            }
        }

        /// <summary>
        /// VolumeY is the y position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        /// 
        [Column("VolumeY")]
        public double VolumeY
        {
            get
            {
                return modelObj.VolumePosition.Y;
            }
        }

        [Column("Radius")]
        public double Radius
        {
            get { return modelObj.Radius; }
            set
            {
                if (modelObj.Radius == value)
                    return;

                modelObj.Radius = value;
            }
        }

        [Column("TypeCode")]
        public LocationType TypeCode
        {
            get { return (LocationType)modelObj.TypeCode; }
        }

        /// <summary>
        /// This column is set to true when the location has one link and is not marked as terminal.  It means the
        /// Location is a dead-end and the user did not mark it as a dead end, which means it may actually continue
        /// and the user was distracted
        /// </summary>
        /// 
        [Column("IsUnverifiedTerminal")]
        public bool IsUnverifiedTerminal
        {
            get
            {
                return modelObj.IsUnverifiedTerminal;
            }
        }

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        /// 

        [Column("Section")]
        public int Section
        {
            get { return (int)modelObj.Section; }
        }

        /// <summary>
        /// Return true if the locations volume position has been calculated
        /// </summary>
        public bool VolumePositionHasBeenCalculated
        {
            get { return this.modelObj.VolumePositionHasBeenCalculated; }
        }

        #endregion

    }
}