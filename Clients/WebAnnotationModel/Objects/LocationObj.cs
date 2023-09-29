using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{

    public static class LocationTypeExtensions
    {
        public static bool AllowsClosed2DShape(this LocationType value)
        {
            switch (value)
            {
                case LocationType.POLYGON:
                case LocationType.CURVEPOLYGON:
                case LocationType.CLOSEDCURVE:
                    return true;
                default:
                    return false;
            }
        }

        public static bool AllowsInteriorHoles(this LocationType value)
        {
            switch (value)
            {
                case LocationType.POLYGON:
                case LocationType.CURVEPOLYGON:
                    return true;
                default:
                    return false;
            }
        }

        public static bool AllowsOpen2DShape(this LocationType value)
        {
            switch (value)
            {
                case LocationType.POLYLINE:
                case LocationType.OPENCURVE:
                    return true;
                default:
                    return false;
            }
        }
    }

    public class LocationObj : WCFObjBaseWithKey<long, Location>, ILocation
    {
        public static bool IsPositionProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return true;

            switch (propertyName)
            {
                case "Position":
                    return true;
                case "WorldPosition":
                    return true;
                //case "VolumePosition":
                //    return true;
                //case "VolumeShape":
                //  return true;
                case "MosaicShape":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsGeometryProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return true;

            switch (propertyName)
            {
                //case "VolumeShape":
                //  return true;
                case "MosaicShape":
                    return true;
                case "Radius":
                    return true;
                case "Width":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsTerminalProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return true;

            switch (propertyName)
            {
                case "Terminal":
                    return true;
                case "OffEdge":
                    return true;
                case "Attributes":
                    return true;
                default:
                    return false;
            }
        }

        public override long ID
        {
            get { return Data.ID; }
        }

        /// <summary>
        /// The ID for newo bjects can change from a negative number to the number in the database.
        /// In this case make sure we always return the same hash code.  As a result this is called for each object only once.
        /// </summary>
        /// <returns></returns>
        protected override int GenerateHashCode()
        {
            return (int)(ID % int.MaxValue);
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
            get { return Data.ParentID; }
        }

        // private StructureObj _Parent;
        public StructureObj Parent
        {
            get
            {
                //       if (_Parent != null)
                //                    return _Parent;

                if (ParentID.HasValue == false)
                    return null;

                StructureObj _Parent = Store.Structures.GetObjectByID(ParentID.Value, false);

                //Queue a request for later
                if (_Parent == null)
                {
                    System.Threading.Tasks.Task.Factory.StartNew(() => Store.Structures.GetObjectByID(ParentID.Value));
                    //Action<long> request = new Action<long>((ID) => Store.Structures.GetObjectByID(ID));
                    //request.BeginInvoke(ParentID.Value, null, null); 
                }

                return _Parent;
            }
        }


        private GridVector2? _MosaicPosition;

        public GridVector2 Position
        {
            get
            {

                if (!_MosaicPosition.HasValue)
                {
                    _MosaicPosition = CenterOfLocationShape(this.TypeCode, this.MosaicShape);
                    //_MosaicPosition = new GridVector2(Data.Position.X, Data.Position.Y);
                }
                /*

                if (!_MosaicPosition.HasValue)
                    _MosaicPosition = this.MosaicShape.Centroid();

                return this.Data.MosaicShape.ToCentroid(); 
                */

                return _MosaicPosition.Value;


            }
            /*
            set
            {
                if (GridVector2.Equals(this.Position, value))
                    return;

                OnPropertyChanging("Position");

                AnnotationPoint point = new AnnotationPoint();
                point.X = value.X;
                point.Y = value.Y;
                point.Z = Data.Position.Z;
                Data.Position = point;
                _MosaicPosition = value; 
                OnPropertyChanged("Position");

                SetDBActionForChange();
            }*/
        }


        private GridVector2? _VolumePosition;
        /// <summary>
        /// VolumeX is the x position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        public GridVector2 VolumePosition
        {
            get
            {

                if (!_VolumePosition.HasValue)
                {
                    _VolumePosition = CenterOfLocationShape(this.TypeCode, this.VolumeShape);
                    //_VolumePosition = Data.VolumeShape.Centroid();
                    //_VolumePosition = new GridVector2(Data.VolumePosition.X, Data.VolumePosition.Y);
                }
                /*
                if (!_VolumePosition.HasValue)
                    _VolumePosition = this.VolumeShape.Centroid();
                */
                return _VolumePosition.Value;
            }

            /*
            set
            {
                if (GridVector2.Equals(this.VolumePosition, value))
                    return;

                OnPropertyChanging("VolumePosition");

                AnnotationPoint point = new AnnotationPoint();
                point.X = value.X;
                point.Y = value.Y;
                point.Z = Data.Position.Z;
                Data.VolumePosition = point;
                _VolumePosition = value;
                OnPropertyChanged("VolumePosition");

                //                SetDBActionForChange();
            }*/

        }

        private static GridVector2 CenterOfLocationShape(LocationType type, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            switch (type)
            {
                case LocationType.POINT:
                case LocationType.CIRCLE:
                case LocationType.ELLIPSE:
                    return shape.BoundingBox().Center;
                default:
                    return shape.Centroid();
            }
        }

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        public double Z
        {
            get { return Data.Position.Z; }
        }

        private Microsoft.SqlServer.Types.SqlGeometry _VolumeShape;
        public Microsoft.SqlServer.Types.SqlGeometry VolumeShape
        {
            get
            {
                if (_VolumeShape == null && Data.VolumeShapeWKB != null)
                {
                    //_VolumeShape = Data.VolumeShape.ToSqlGeometry();
                    _VolumeShape = Data.VolumeShapeWKB.ToSqlGeometry();
                }
                return _VolumeShape;
            }
            set
            {
                Debug.Assert(value != null);
                if (value == null)
                    return;

                //                DbGeometry newValue = value.ToDbGeometry();
                if (VolumeShape != null && VolumeShape.SpatialEquals(value)) return;

                OnPropertyChanging("VolumeShape");

                OnPropertyChanging("VolumePosition");
                _VolumePosition = value.Centroid();
                OnPropertyChanged("VolumePosition");

                //Data.VolumeShape = newValue;
                Data.VolumeShapeWKB = value.AsBinary();
                _VolumeShape = value;
                OnPropertyChanged("VolumeShape");

                SetDBActionForChange();
            }

        }

        private Microsoft.SqlServer.Types.SqlGeometry _MosaicShape;
        public Microsoft.SqlServer.Types.SqlGeometry MosaicShape
        {
            get
            {
                if (_MosaicShape == null && Data.MosaicShapeWKB != null)
                {
                    _MosaicShape = Data.MosaicShapeWKB.ToSqlGeometry();
                }
                return _MosaicShape;
            }
            set
            {
                Debug.Assert(value != null);
                if (value == null)
                    return;

                //DbGeometry newValue = value.ToDbGeometry();
                if (MosaicShape != null && MosaicShape.SpatialEquals(value)) return;

                OnPropertyChanging("MosaicShape");

                OnPropertyChanging("Position");
                _MosaicPosition = value.Centroid();
                OnPropertyChanged("Position");

                //Data.MosaicShape = newValue;
                Data.MosaicShapeWKB = value.AsBinary();
                _MosaicShape = null;
                OnPropertyChanged("MosaicShape");

                OnPropertyChanging("Radius");
                _Radius = CalculateRadius(value);
                OnPropertyChanged("Radius");

                SetDBActionForChange();
            }
        }


        /// <summary>
        /// Record the hashcode of the volume transform used to map the location. 
        /// </summary>
        public long? VolumeTransformID = new int?();

        /// <summary>
        /// Return true if the location's volume position has not yet been mapped by this Viking client
        /// </summary>
        public bool VolumePositionHasBeenCalculated
        {
            get { return this.VolumeTransformID.HasValue; }
        }

        public void ResetVolumePositionHasBeenCalculated()
        {
            this.VolumeTransformID = new int?();
        }

        private double CalculateRadius(Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            if (shape.STDimension() == 0)
            {
                return 8;
            }
            else if (shape.STDimension() == 1)
            {
                return shape.STLength().Value / 2.0;
            }
            else if (shape.STDimension() == 2)
            {
                return Math.Sqrt(shape.STArea().Value / Math.PI);
            }
            else
                return this.Width.Value / 2.0;
        }

        private double CalculateRadius(System.Data.Entity.Spatial.DbGeometry shape)
        {
            if (shape.Dimension == 1)
            {
                return shape.Length.Value / 2.0;
            }
            else if (shape.Dimension == 2)
                return Math.Sqrt(shape.Area.Value / Math.PI);
            else
                return this.Width.Value / 2.0;
        }

        private double? _Radius;
        public double Radius
        {
            get
            {
                if (!_Radius.HasValue)
                {
                    _Radius = CalculateRadius(MosaicShape);
                    Debug.Assert(_Radius > 0);
                }

                return _Radius.Value;
            }
            /*
            set {
                if (Data.Radius == value)
                    return;

                OnPropertyChanging("Radius");
                Data.Radius = value;
                OnPropertyChanged("Radius");

                if (this.TypeCode == LocationType.CIRCLE)
                {
                    this.MosaicShape = SqlGeometryUtils.GeometryExtensions.ToCircle(this.Position.X,
                                           this.Position.Y,
                                           this.Z,
                                           value);

                    this.VolumeShape = SqlGeometryUtils.GeometryExtensions.ToCircle(this.VolumePosition.X,
                                           this.VolumePosition.Y,
                                           this.Z,
                                           value);
                }
                 
                SetDBActionForChange();
            }
            */
        }

        private const double g_MinimumWidth = 1.0;
        public double? Width
        {
            get
            {
                if (Data.Width.HasValue && Data.Width < g_MinimumWidth)
                {
                    return g_MinimumWidth;
                }
                else if (Data.Width.HasValue == false)
                {
                    return g_MinimumWidth;
                }

                return Data.Width;
            }
            set
            {
                if (Data.Width == value)
                    return;

                OnPropertyChanging("Width");
                Data.Width = value;
                OnPropertyChanged("Width");

                SetDBActionForChange();
            }
        }

        public LocationType TypeCode
        {
            get { return (LocationType)Data.TypeCode; }
            set
            {
                if (Data.TypeCode == (short)value)
                    return;

                OnPropertyChanging("TypeCode");
                Data.TypeCode = (short)value;
                SetDBActionForChange();
                OnPropertyChanged("TypeCode");
            }
        }

        /// <summary>
        /// This column is set to true when the location has one link and is not marked as terminal.  It means the
        /// Location is a dead-end and the user did not mark it as a dead end, which means it may actually continue
        /// and the user was distracted
        /// </summary>
        public bool IsUnverifiedTerminal
        {
            get
            {
                if (NumLinks >= 2)
                    return false;
                return !(Terminal || OffEdge || VericosityCap || Untraceable);
            }
        }

        /// <summary>
        /// This should return true when we know no further annotation will proceed from this point
        /// </summary>
        public bool IsVerifiedTerminal
        {
            get
            {
                return (Terminal || OffEdge || VericosityCap || Untraceable);
            }
        }



        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        public int Section
        {
            get { return (int)Data.Section; }
        }

        /// <summary>
        /// Name of the last user to edit the location
        /// </summary>
        public string Username
        {
            get { return Data.Username; }
        }

        private readonly object LinkLock = new object();

        private ObservableCollection<long> _ObservableLinks = null;
        private ReadOnlyObservableCollection<long> _ReadOnlyObservableLinks = null;

        public long[] LinksCopy
        {
            get
            {
                lock (LinkLock)
                {
                    if (_ObservableLinks == null)
                        return new long[0];

                    return _ObservableLinks.ToArray();
                }
            }
        }

        /// <summary>
        /// This needs sorting out.  Do we need this as an observable collection or should 
        /// we fire our own collection changed events with Add/Remove link calls.
        /// </summary>
        public ReadOnlyObservableCollection<long> Links
        {
            get
            {
                lock (LinkLock)
                {
                    if (_ObservableLinks == null)
                    {
                        if (Data.Links != null)
                        {
                            _ObservableLinks = new ObservableCollection<long>(Data.Links);
                        }
                        else
                        {
                            _ObservableLinks = new ObservableCollection<long>();
                        }

                        _ReadOnlyObservableLinks = new ReadOnlyObservableCollection<long>(_ObservableLinks);
                    }

                    return _ReadOnlyObservableLinks;
                    /*
                    return new ReadOnlyObservableCollection<long>(_Links); 
                    if (_Links == null)
                    {
                        //Initialize from the Data object
                        if (Data.Links == null)
                        {
                            _Links = new ObservableCollection<long>();
                            _Links.CollectionChanged += this.OnLinksChanged;
                        }
                        else
                        {
                            _Links = new ObservableCollection<long>(Data.Links);
                            _Links.CollectionChanged += this.OnLinksChanged;
                        }
                    }

                    return _Links;
                    */
                }
            }
        }

        /// <summary>
        /// The number of locations linked to this annotation.
        /// </summary>
        public int NumLinks
        {
            get
            {
                if (_ObservableLinks == null)
                {
                    if (Data.Links == null)
                        return 0;
                    else
                        return Data.Links.Length;
                }
                else
                {
                    //Debug.Assert(Data.Links.Length == Links.Count);
                    return _ObservableLinks.Count;
                }
            }
        }

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        internal void AddLink(long ID)
        {
            if (ID == this.ID)
                throw new ArgumentException("Can't add own ID from location links");

            lock (LinkLock)
            {
                if (Links.Contains(ID))
                    return;

                _ObservableLinks.Add(ID);

                this.Data.Links = this._ObservableLinks.ToArray();
            }
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// </summary>
        /// <param name="ID"></param>
        internal void RemoveLink(long ID)
        {
            if (ID == this.ID)
                throw new ArgumentException("Can't remove own ID from location links");

            lock (LinkLock)
            {
                if (!Links.Contains(ID))
                    return;

                _ObservableLinks.Remove(ID);

                if (_ObservableLinks.Count > 0)
                    this.Data.Links = this._ObservableLinks.ToArray();
                else
                    this.Data.Links = null;

            }
        }

        /// <summary>
        /// True if the location marks where a structure process ends as part of normal biology
        /// </summary>
        public bool Terminal
        {
            get { return Data.Terminal; }
            set
            {
                if (Data.Terminal == value)
                    return;

                OnPropertyChanging("Terminal");
                Data.Terminal = value;
                SetDBActionForChange();
                OnPropertyChanged("Terminal");
            }
        }

        /// <summary>
        /// True if the location marks where a structure goes off the edge of a volume
        /// </summary>
        public bool OffEdge
        {
            get { return Data.OffEdge; }
            set
            {
                if (Data.OffEdge == value)
                    return;

                OnPropertyChanging("OffEdge");
                Data.OffEdge = value;
                SetDBActionForChange();
                OnPropertyChanged("OffEdge");
            }
        }

        /// <summary>
        /// True if the location is a vericosity cap, this terminates a process in a structure
        /// </summary>
        public bool VericosityCap
        {
            get { return Attributes.Any(a => a.Name == "Varicosity Cap"); }
        }
        
        /// <summary>
        /// True if the location indicates a boundary beyond which the structure cannot be traced
        /// </summary>
        public bool Untraceable
        {
            get { return Attributes.Any(a => a.Name == "Untraceable"); }
        }

        public DateTime LastModified
        {
            get { return new DateTime(Data.LastModified, DateTimeKind.Utc); }
        }

        List<ObjAttribute> _Attributes = null;

        public IEnumerable<ObjAttribute> Attributes
        {
            get
            {

                if (_Attributes == null)
                {
                    _Attributes = ObjAttribute.Parse(Data.AttributesXml);
                }

                return _Attributes;
            }
            set
            {
                if (Data.AttributesXml == null && value == null)
                    return;

                string xmlstring = ObjAttribute.ToXml(value);

                if (xmlstring == "")
                    xmlstring = null;

                if (Data.AttributesXml != xmlstring)
                {
                    OnPropertyChanging("Attributes");

                    Data.AttributesXml = xmlstring;
                    _Attributes = null;

                    //Refresh the tags
                    SetDBActionForChange();
                    OnPropertyChanged("Attributes");
                }
            }
        }

        ulong ILocation.ID => (ulong)this.ID;

        ulong ILocation.ParentID => (ulong)this.ParentID;

        bool ILocation.Terminal => this.Terminal;

        bool ILocation.OffEdge => this.OffEdge;

        bool ILocation.IsVericosityCap => this.VericosityCap;

        bool ILocation.IsUntraceable => this.Untraceable;

        IDictionary<string, string> ILocation.Attributes => this.Attributes.ToDictionary(i => i.Name, i => i.Value);

        long ILocation.UnscaledZ => (long)this.Data.Position.Z;

        string ILocation.TagsXml => this.Data.AttributesXml;

        LocationType ILocation.TypeCode => this.TypeCode;

        double ILocation.Z => throw new NotImplementedException(); //Need to know scale of volume

        SqlGeometry ILocation.Geometry => this.VolumeShape;

        /// <summary>
        /// Add the specified name to the attributes if it does not exists, removes it 
        /// </summary>
        /// <param name="tag"></param>
        public bool ToggleAttribute(string tag, string value = null)
        {
            ObjAttribute attrib = new ObjAttribute(tag, value);
            List<ObjAttribute> listAttributes = this.Attributes.ToList();
            bool InList = listAttributes.ToggleAttribute(tag, value);
            this.Attributes = listAttributes;
            return InList;
        }

        public LocationObj()
        {
            Data = new Location();
        }

        public LocationObj(Location obj)
        {
            Data = obj;
        }

        public LocationObj(StructureObj parent,
                           int SectionNumber, LocationType shapeType)
        {
            this.Data = new Location
            {
                DBAction = AnnotationService.Types.DBACTION.INSERT,
                ID = Store.Locations.GetTempKey(),
                TypeCode = (short)shapeType
            };

            if (shapeType == LocationType.CIRCLE)
                this.Data.Radius = 16;

            if (shapeType == LocationType.POINT)
                this.Data.Radius = 16;

            this.Data.Links = null;

            //this.Data.MosaicShape = mosaicShape.ToDbGeometry();
            //this.Data.VolumeShape = volumeShape.ToDbGeometry();

            this.Data.Section = SectionNumber;

            if (parent != null)
            {
                this.Data.ParentID = parent.ID;
            }

            //          CallOnCreate(); 
        }

        public LocationObj(StructureObj parent,
                            Microsoft.SqlServer.Types.SqlGeometry mosaicShape, Microsoft.SqlServer.Types.SqlGeometry volumeShape,
                           int SectionNumber, LocationType shapeType) : this(parent, SectionNumber, shapeType)
        {
            //this.Data.MosaicShape = mosaicShape.ToDbGeometry();
            //this.Data.VolumeShape = volumeShape.ToDbGeometry();

            this.Data.MosaicShapeWKB = mosaicShape.AsBinary();
            this.Data.VolumeShapeWKB = volumeShape.AsBinary();
        }


        /// <summary>
        /// Override and write each property individually so we send specific property changed events
        /// </summary>
        /// <param name="newdata"></param>
        internal override void Update(Location newdata)
        {
            Debug.Assert(this.Data.ID == newdata.ID);
            this.Data.DBAction = AnnotationService.Types.DBACTION.NONE;
            this.Data.Closed = newdata.Closed;
            this.Data.TypeCode = newdata.TypeCode;
            this.Data.Position = newdata.Position;
            this.Data.VolumePosition = newdata.VolumePosition;
            this.Data.Radius = newdata.Radius;
            this.Data.Section = newdata.Section;
            this.Data.Terminal = newdata.Terminal;
            this.Data.OffEdge = newdata.OffEdge;
            this.Data.ParentID = newdata.ParentID;
            this.Data.Username = newdata.Username;
            this.Data.LastModified = newdata.LastModified;
            this.Data.Links = newdata.Links;
            this._Attributes = null;
            this._VolumeShape = null;
            this._MosaicShape = null;
            this._VolumePosition = default;
            this._MosaicPosition = default;
            this.Data.VolumeShapeWKB = newdata.VolumeShapeWKB;
            this.Data.MosaicShapeWKB = newdata.MosaicShapeWKB;
        }


        /*
        public override void Delete()
        {
            DBACTION originalAction = this.DBAction; 
            this.DBAction = DBACTION.DELETE;

            bool success = Store.Locations.Save();
            if(!success)
            {
                //Write straight to data since we have an assert to check whether an object is being deleted, but
                //in this case we know it is ok
                this.Data.DBAction = originalAction;
            }


            if (this.ParentID.HasValue)
                Store.Structures.CheckForOrphan(this.ParentID.Value);
        }
        */

        protected static event EventHandler OnCreate;
        protected void CallOnCreate()
        {
            //Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, new object[] { this, null });
            OnCreate?.Invoke(this, null);
        }

        bool IEquatable<ILocation>.Equals(ILocation other)
        {
            if (other is null)
                return false;

            return other.ID == (ulong)this.ID;
        }

        public static event EventHandler Create
        {
            add { OnCreate += value; }
            remove { OnCreate -= value; }
        }
    }
}
