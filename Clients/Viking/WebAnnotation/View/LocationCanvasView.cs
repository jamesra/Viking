using System;
using System.Diagnostics;
using System.Windows;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using WebAnnotationModel;
using WebAnnotation.ViewModel;
using Geometry;
using Microsoft.SqlServer.Types;
using VikingXNA;
using System.Windows.Forms;
using Viking.Common;

namespace WebAnnotation.View
{
    public interface ISelectable
    {
        bool Selected { get; set; }
    }

    public interface ICanvasView
    { 
        bool IsVisible(Scene scene);

        /// <summary>
        /// Bounding box of the annotation
        /// </summary>
        GridRectangle BoundingBox
        {
            get;
        } 

        bool Intersects(GridVector2 Position);

        /// <summary>
        /// Returns the distance from the position to the nearest point on the annotation, or 0 if the position is inside the annotation
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double Distance(GridVector2 Position);

        /// <summary>
        /// Assumes Position is within the annotation.  Returns a number from 0 to 1 indicating how close the position is between the center and edge of the annotation.
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double DistanceFromCenterNormalized(GridVector2 Position);
    }

    abstract public class LocationCanvasView : IComparable<LocationCanvasView>, IWeakEventListener, IUIObjectBasic, ICanvasView
    {
        public readonly LocationObj modelObj;

        public LocationCanvasView(LocationObj obj)
        {
            this.modelObj = obj;
        }

        /// <summary>
        /// The number of parent structures until we hit a root structure
        /// </summary>
        private int? _ParentDepth = new int?();
        public int ParentDepth
        {
            get
            {
                if(!_ParentDepth.HasValue)
                {
                    _ParentDepth = CalculateParentDepth(modelObj.Parent);
                }

                return _ParentDepth.Value;
            }
        }

        private int CalculateParentDepth(StructureObj obj)
        {
            if (obj == null)
                return 0;

            return CalculateParentDepth(obj.Parent) + 1; 
        }

        public abstract bool Intersects(SqlGeometry shape);
                 

        public abstract void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              Scene scene,
                              float MagnificationFactor,
                              int DirectionToVisiblePlane);

        public abstract LocationAction GetActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber);


        public abstract IList<LocationCanvasView> OverlappingLinks
        {
            get;
        }

        public long ID
        {
            get { return modelObj.ID; }
        }

        public double Z
        {
            get { return modelObj.Z; }
        }

        public LocationType TypeCode
        {
            get { return modelObj.TypeCode; }
        }

        public bool IsTerminal
        {
            get { return modelObj.Terminal; }
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

        public ICollection<long> Links
        {
            get { return modelObj.Links; }
        }
                
        public GridVector2 VolumePosition
        {
            get
            {
                return modelObj.VolumePosition;
            }
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
            LocationCanvasView LocObj = obj as LocationCanvasView;
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

        public static bool operator ==(LocationCanvasView A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(LocationCanvasView A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if ((object)A != null)
                return !A.Equals(B);

            return true;
        }

        public long? ParentID
        {
            get { return modelObj.ParentID; }
        }

        public ContextMenu ContextMenu
        {
            get
            {
                Location_CanvasContextMenuView contextView = new Location_CanvasContextMenuView(this.modelObj);
                return contextView.ContextMenu;
            }
        }

        public string ToolTip
        {
            get
            {
                return this.modelObj.Label; 
            }
        }

        public bool Equals(LocationCanvasView x, LocationCanvasView y)
        {
            if (x == null && y == null)
                return true;

            if (x == null || y == null)
                return false;

            return x.ID == y.ID;
        }

        public int GetHashCode(LocationCanvasView obj)
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

        int IComparable<LocationCanvasView>.CompareTo(LocationCanvasView other)
        {
            if (other == null)
                return 1;

            return (int)(this.ID - other.ID);
        }

        /// <summary>
        /// Return true if all of the locations are present in the local store
        /// </summary>
        public bool AllLinksLoaded
        {
            get
            {
                ICollection<LocationObj> listLinkedLocations = Store.Locations.GetObjectsByIDs(this.Links, false);
                return listLinkedLocations.Count == this.Links.Count;
            }
        }

        public abstract GridRectangle BoundingBox { get; }

        #region Weak Events
        private object EventsLock = new object();
        private bool EventsRegistered = false;
        private bool LinkedEventsRegistered = false;
        private bool StructureEventsRegistered = false;

        internal void RegisterForLocationEvents()
        {
            if (EventsRegistered)
                return;

            lock (EventsLock)
            {
                if (EventsRegistered)
                    return;

                NotifyPropertyChangedEventManager.AddListener(this.modelObj, this);
                 
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
                
                EventsRegistered = false;
            }
        }

        internal void RegisterForLinkedLocationChangeEvents()
        {
            if (AllLinksLoaded)
            {
                lock (EventsLock)
                {
                    if (LinkedEventsRegistered)
                        return;

                    ICollection<LocationObj> listLinkedLocations = Store.Locations.GetObjectsByIDs(this.Links, false);
                    foreach (LocationObj loc in listLinkedLocations)
                    {
                        NotifyPropertyChangedEventManager.AddListener(loc, this);
                    }

                    LinkedEventsRegistered = true;
                }
            }
        }

        internal void DeregisterForLinkedLocationChangeEvents()
        {
            if (AllLinksLoaded)
            {
                lock (EventsLock)
                {
                    if (!LinkedEventsRegistered)
                        return;

                    ICollection<LocationObj> listLinkedLocations = Store.Locations.GetObjectsByIDs(this.Links, false);
                    foreach (LocationObj loc in listLinkedLocations)
                    {
                        NotifyPropertyChangedEventManager.RemoveListener(loc, this);
                    }

                    LinkedEventsRegistered = false; 
                }
            }
        }

        internal void RegisterForStructureChangeEvents()
        {
            lock (EventsLock)
            {
                if (StructureEventsRegistered)
                    return;

                if (this.modelObj.Parent == null)
                {
                    Action<long> GetParent = delegate (long ParentID)
                    {
                        StructureObj parent = Store.Structures.GetObjectByID(ParentID, true);
                        if (parent != null)
                            NotifyPropertyChangedEventManager.AddListener(this.modelObj.Parent, this);
                    };

                    AnnotationOverlay.CurrentOverlay.Parent.BeginInvoke(GetParent, new object[] { this.modelObj.ParentID.Value });
                }
                else
                    NotifyPropertyChangedEventManager.AddListener(this.modelObj.Parent, this);

                StructureEventsRegistered = true;
            }
        }

        internal void DeregisterForStructureChangeEvents()
        {
            lock (EventsLock)
            {
                if (!StructureEventsRegistered)
                    return;

                NotifyPropertyChangedEventManager.RemoveListener(this.modelObj.Parent, this);

                StructureEventsRegistered = false;
            }
        }

        #endregion


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
                    LocationObj locObj = sender as LocationObj;
                    if (locObj.ID == this.ID)
                        this.OnObjPropertyChanged(sender, PropertyChangedArgs);
                    else
                        this.OnLinkedObjectPropertyChanged(sender, PropertyChangedArgs);
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

        protected virtual void OnLinkedObjectPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            return;
        }

        protected virtual void OnLinksChanged(object o, NotifyCollectionChangedEventArgs args)
        {
            return;
        }

        public void ShowProperties()
        {
            Location_CanvasContextMenuView contextView = new Location_CanvasContextMenuView(this.modelObj);
            contextView.ShowProperties();
        }

        public void Save()
        {
            throw new NotImplementedException();
        }

        public abstract bool IsVisible(Scene scene);
        public abstract bool Intersects(GridVector2 Position);
        public abstract double Distance(GridVector2 Position);
        public abstract double DistanceFromCenterNormalized(GridVector2 Position);
        #endregion
    }
}
