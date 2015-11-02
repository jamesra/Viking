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

namespace WebAnnotation.View
{
    abstract public class LocationCanvasView : IComparable<LocationCanvasView>, IWeakEventListener
    {
        public readonly LocationObj modelObj;

        public LocationCanvasView(LocationObj obj)
        {
            this.modelObj = obj;
        }

        public abstract bool IsVisible(Scene scene);

        public abstract GridRectangle BoundingBox
        {
            get;
        }

        public abstract bool Intersects(GridVector2 Position);

        public abstract double Distance(GridVector2 Position);

        public abstract void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              Microsoft.Xna.Framework.Vector2 LocationCenterScreenPosition,
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

        public GridVector2 SectionPosition
        {
            get
            {
                return modelObj.Position;
            }
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
    }
}
