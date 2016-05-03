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
using Viking.VolumeModel;
using SqlGeometryUtils;

namespace WebAnnotation.View
{
    abstract public class LocationCanvasView : IComparable<LocationCanvasView>,  IUIObjectBasic, ICanvasView, IEquatable<LocationCanvasView>, IMouseActionSupport
    {
        protected readonly LocationObj modelObj;
         
        public abstract SqlGeometry VolumeShapeAsRendered { get; }

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


        public abstract LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID);
        
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

        public bool OffEdge
        {
            get { return modelObj.OffEdge; }
        }

        public bool IsVericosityCap
        {
            get { return modelObj.VericosityCap; }
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
        
        public abstract ICollection<long> OverlappedLinks
        {
            protected get;
            set;
        }   
                   
        public override string ToString()
        {
            return modelObj.ToString();
        }

        protected string StructureIDLabelWithTypeCode()
        {
            return this.Parent.Type.Code + " " + this.ParentID.ToString();
        }

        /// <summary>
        /// Full label and tag text
        /// </summary>
        /// <returns></returns>
        protected string FullLabelText()
        {
            string fullLabel = this.StructureLabel();

            if (fullLabel.Length == 0)
                fullLabel = this.TagLabel();
            else
                fullLabel += '\n' + this.TagLabel();

            return fullLabel;
        }

        protected string StructureLabel()
        {
            string InfoLabel = "";
            if (Parent.InfoLabel != null)
                InfoLabel = Parent.InfoLabel.Trim();

            return InfoLabel;
        }

        protected string TagLabel()
        {
            string InfoLabel = "";
            foreach (ObjAttribute tag in Parent.Attributes)
            {
                InfoLabel += tag.ToString() + " ";
            }

            foreach (ObjAttribute tag in modelObj.Attributes)
            {
                InfoLabel += tag.ToString() + " ";
            }

            return InfoLabel.Trim();
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
        

        internal virtual void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            this.ResetParentCache();
            return;
        }

        internal virtual void OnObjPropertyChanging(object o, PropertyChangingEventArgs args)
        {
            return;
        }

        internal virtual void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
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

        public virtual bool Intersects(GridVector2 Position)
        {
            return this.VolumeShapeAsRendered.Intersects(Position);
        }

        public virtual bool Intersects(SqlGeometry shape)
        {
            return this.VolumeShapeAsRendered.STIntersects(shape).IsTrue;
        }

        public virtual double Distance(GridVector2 Position)
        {
            return this.VolumeShapeAsRendered.Distance(Position);
        }

        public virtual double Distance(SqlGeometry Shape)
        {
            return this.VolumeShapeAsRendered.STDistance(Shape).Value;
        }

        public abstract bool IsVisible(Scene scene);
        public abstract double DistanceFromCenterNormalized(GridVector2 Position);
        
        public bool Equals(LocationCanvasView other)
        {
            if ((object)other == null)
                return false;

            return other.ID == this.ID;
        }
    }
}
