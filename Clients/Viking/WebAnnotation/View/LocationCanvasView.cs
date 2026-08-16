using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif
using Viking.Common;
using VikingXNA;
using WebAnnotation.UI;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.View
{
#if NETFRAMEWORK
    public delegate ContextMenuStrip ContextMenuGeneratorDelegate(IViewLocation locationID);
#endif

    public abstract class LocationCanvasView(LocationObj obj) : IComparable<LocationCanvasView>, IUIObjectBasic, ICanvasGeometryView, IEquatable<LocationCanvasView>,
                                               IMouseActionSupport, IPenActionSupport, IViewLocation, IHelpStrings
#if NETFRAMEWORK
                                               , IContextMenu
#endif
    {
        #region static

        /// <summary>
        /// Optional accessor function to get the current smallest rendered size setting.
        /// If null, the smallest rendered size check is skipped.
        /// </summary>
        public static Func<double> SmallestRenderedSizeAccessor { get; set; }

        /// <summary>
        /// Return true if a polygon with the given bounding box would be visible if rendered into the scene.
        /// Uses the smallest dimension (min of width and height) to determine visibility.
        /// </summary>
        /// <param name="boundingBox">Bounding box in world coordinates</param>
        /// <param name="scene">Scene to check visibility against</param>
        /// <returns>True if the polygon would be visible</returns>
        public static bool IsPolygonVisible(Rectangle boundingBox, VikingXNA.Scene scene)
        {
            // Check if bounding box intersects visible world bounds
            if (!scene.VisibleWorldBounds.Intersects(boundingBox))
                return false;

            // Check smallest rendered size if accessor is provided
            if (SmallestRenderedSizeAccessor != null)
            {
                double smallestDimension = Math.Min(boundingBox.Width, boundingBox.Height);
                double scaledSmallestDimension = smallestDimension / scene.Camera.Downsample;
                double smallestRenderedSize = SmallestRenderedSizeAccessor();
                if (scaledSmallestDimension < smallestRenderedSize)
                    return false;
            }

            return true;
        }

        #endregion

        protected readonly LocationObj modelObj = obj;

        public abstract SqlGeometry VolumeShapeAsRendered { get; }

#if NETFRAMEWORK
        public readonly ContextMenuGeneratorDelegate ContextMenuGenerator = Location_CanvasContextMenuView.ContextMenuGenerator;
#endif

        public int VisualHeight => ParentDepth;

        /// <summary>
        /// The number of parent structures until we hit a root structure
        /// </summary>
        private int? _ParentDepth = new int?();
        public int ParentDepth
        {
            get
            {
                if (!_ParentDepth.HasValue)
                {
                    _ParentDepth = CalculateParentDepth(modelObj.Parent);
                }

                return _ParentDepth.Value;
            }
        }

        private const int MaxParentDepth = 128;

        private static int CalculateParentDepth(StructureObj obj)
        {
            if (obj is null)
            {
                return 0;
            }

            int depth = 0;
            HashSet<long> visited = [];
            StructureObj current = obj;

            while (current is not null)
            {
                if (!visited.Add(current.ID))
                {
                    return depth;
                }

                depth++;
                if (depth >= MaxParentDepth)
                {
                    return depth;
                }

                current = current.Parent;
            }

            return depth;
        }


        public abstract LocationAction GetMouseClickActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID);

        public abstract LocationAction GetPenContactActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID);

        public abstract List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber);

        public long ID => modelObj.ID;

        public double Z => modelObj.Z;

        public Viking.AnnotationServiceTypes.Interfaces.LocationType TypeCode => modelObj.TypeCode;

        public bool IsTerminal => modelObj.Terminal;

        public bool OffEdge => modelObj.OffEdge;

        public bool IsVericosityCap => modelObj.VericosityCap;

        private Structure? _Parent = null;
        private void ResetParentCache() => _Parent = null;

        public Structure Parent
        {
            get
            {
                if (modelObj.Parent is null)
                {
                    return null;
                }

                _Parent ??= new Structure(modelObj.Parent);

                return _Parent;
            }
        }

        public ICollection<long> Links => modelObj.Links;

        public abstract ICollection<long> OverlappedLinks
        {
            protected get;
            set;
        }

        public override string ToString() => modelObj.ToString();

        protected string StructureIDLabelWithTypeCode() => Parent.Type.Code + " " + ParentID.ToString();

        /// <summary>
        /// Full label and tag text
        /// </summary>
        /// <returns></returns>
        protected string FullLabelText()
        {
            string fullLabel = StructureLabel();

            if (fullLabel.Length == 0)
            {
                fullLabel = TagLabel();
            }
            else
            {
                fullLabel += '\n' + TagLabel();
            }

            return fullLabel;
        }

        protected string StructureLabel()
        {
            string InfoLabel = "";
            if (Parent?.InfoLabel != null)
            {
                InfoLabel = Parent.InfoLabel.Trim();
            }

            return InfoLabel;
        }

        protected string TagLabel()
        {
            if (Parent is null)
            {
                return "";
            }

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

        protected bool IsLocationPropertyAffectingLabels(string PropertyName)
        {
            return string.IsNullOrEmpty(PropertyName) ||
                PropertyName == "Terminal" ||
                PropertyName == "OffEdge" ||
                PropertyName == "Attributes";
        }

        public override int GetHashCode() => modelObj.GetHashCode();

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

        public static bool operator ==(LocationCanvasView? A, object? B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if (A is not null)
            {
                return A.Equals(B);
            }

            return false;
        }

        public static bool operator !=(LocationCanvasView? A, object? B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if (A is not null)
            {
                return !A.Equals(B);
            }

            return true;
        }

        public long? ParentID => modelObj.ParentID;

#if NETFRAMEWORK
        public ContextMenuStrip ContextMenu
        {
            get
            {
                if (ContextMenuGenerator != null)
                {
                    return ContextMenuGenerator(this);
                }

                return null;
            }
        }
#endif

        public string ToolTip => modelObj.Label;

        public bool Equals(LocationCanvasView x, LocationCanvasView y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.ID == y.ID;
        }

        public int GetHashCode(LocationCanvasView obj)
        {
            if (obj is null)
            {
                throw new ArgumentNullException("obj", "GetHashCode");
            }

            return obj.modelObj.GetHashCode();
        }

        public bool Equals(LocationObj x, LocationObj y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.ID == y.ID;
        }

        public int GetHashCode(LocationObj obj) => obj.GetHashCode();

        int IComparable<LocationCanvasView>.CompareTo(LocationCanvasView other)
        {
            if (other is null)
            {
                return 1;
            }

            return (int)(ID - other.ID);
        }

        /// <summary>
        /// Return true if all of the locations are present in the local store
        /// </summary>
        public bool AllLinksLoaded
        {
            get
            {
                Store.Locations.TryGetObjectsByIDs(Links, out var listLinkedLocations, out _);
                return listLinkedLocations.Count == Links.Count;
            }
        }

        public abstract Rectangle BoundingBox { get; }
        public abstract string[] HelpStrings { get; }

        internal virtual void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            ResetParentCache();
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
#if NETFRAMEWORK
            Location_CanvasContextMenuView contextView = new(ID);
            contextView.ShowProperties();
#endif
        }

        public void Save() => throw new NotImplementedException();

        public virtual bool Contains(Vector2 Position) => VolumeShapeAsRendered.Intersects(Position);

        public virtual bool Intersects(LineSegment line) => VolumeShapeAsRendered.Intersects(line);

        public virtual bool Intersects(SqlGeometry shape) => VolumeShapeAsRendered.STIntersects(shape).IsTrue;

        public virtual double Distance(Vector2 Position) => VolumeShapeAsRendered.Distance(Position);

        public virtual double Distance(SqlGeometry Shape) => VolumeShapeAsRendered.STDistance(Shape).Value;

        public abstract bool IsVisible(Scene scene);
        public abstract double DistanceFromCenterNormalized(Vector2 Position);

        public bool Equals(LocationCanvasView other)
        {
            if (other is null)
            {
                return false;
            }

            return other.ID == ID;
        }


    }
}
