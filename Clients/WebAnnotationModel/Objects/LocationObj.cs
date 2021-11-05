using Viking.AnnotationServiceTypes.Interfaces;
using Geometry; 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel;

namespace WebAnnotationModel.Objects
{

    public static class LocationTypeExtensions
    {
        public static bool HasRadius(this LocationType value)
        {
            switch (value)
            {
                case LocationType.CIRCLE:
                case LocationType.POINT:
                    return true;
                default:
                    return false;
            }
        }

        public static bool HasWidth(this LocationType value)
        {
            switch (value)
            {
                case LocationType.OPENCURVE:
                case LocationType.POLYLINE:
                    return true;
                default:
                    return false;
            }
        }

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

    public class LocationObj : AnnotationModelObjBaseWithKey<long, ILocation>, ISectionIndex, IDataObjectLinks<long, long>, IEquatable<LocationObj>, ILocationReadOnly
    {
        private readonly long _ID;

        public override long ID => _ID;

        public static bool IsPositionProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return true;

            switch (propertyName)
            {
                case nameof(Position):
                    return true;
                case nameof(VolumePosition):
                    return true;
                //case "VolumePosition":
                //    return true;
                //case "VolumeShape":
                //  return true;
                case nameof(MosaicShape):
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
                case nameof(MosaicShape):
                    return true;
                case nameof(Radius):
                    return true;
                case nameof(Width):
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
                case nameof(Terminal):
                    return true;
                case nameof(OffEdge):
                    return true;
                case nameof(Attributes):
                    return true;
                default:
                    return false;
            }
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
            get;
            internal set;
        }

        // private StructureObj _Parent;
        public StructureObj Parent
        {
            get;
            /*
            {
                //       if (_Parent != null)
                //                    return _Parent;

                if (ParentID.HasValue == false)
                    return null;

                StructureObj _Parent = Store.Structures.GetObjectByID(ParentID.Value, false).Result;

                //Queue a request for later
                if (_Parent == null)
                {
                    Store.Structures.GetObjectByID(ParentID.Value);
                    //Action<long> request = new Action<long>((ID) => Store.Structures.GetObjectByID(ID));
                    //request.BeginInvoke(ParentID.Value, null, null); 
                }

                return _Parent;
            }*/
        }


        private GridVector2? _MosaicPosition;

        public GridVector2 Position
        {
            get
            {

                if (!_MosaicPosition.HasValue)
                {
                    _MosaicPosition = CenterOfLocationShape(this.MosaicShape);
                    //_MosaicPosition = new GridVector2(Data.Position.X, Data.Position.Y);
                }
                /*

                if (!_MosaicPosition.HasValue)
                    _MosaicPosition = this.MosaicShape.Centroid();

                return this.Data.MosaicShape.ToCentroid(); 
                */

                return _MosaicPosition.Value;


            }
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
                    _VolumePosition = CenterOfLocationShape(this.VolumeShape);
                    //_VolumePosition = Data.VolumeShape.Centroid();
                    //_VolumePosition = new GridVector2(Data.VolumePosition.X, Data.VolumePosition.Y);
                }
                /*
                if (!_VolumePosition.HasValue)
                    _VolumePosition = this.VolumeShape.Centroid();
                */
                return _VolumePosition.Value;
            }

        }

        private static GridVector2 CenterOfLocationShape(IShape2D shape)
        {
            if (shape is ICentroid c)
                return c.Centroid.ToGridVector2();

            return shape.BoundingBox.Center;
        }

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        public double Z => Section;

        private IShape2D _VolumeShape;
        public IShape2D VolumeShape
        {
            get => _VolumeShape;
            set
            {
                Debug.Assert(value != null);
                if (value == null)
                    return;

                //                DbGeometry newValue = value.ToDbGeometry();
                if (VolumeShape != null && VolumeShape.Equals(value)) return;

                OnPropertyChanging(nameof(VolumeShape));

                OnPropertyChanging(nameof(VolumePosition));
                if (value is ICentroid c)
                    _VolumePosition = c.Centroid.ToGridVector2();
                else
                    _VolumePosition = value.BoundingBox.Center;
                OnPropertyChanged(nameof(VolumePosition));

                //Data.VolumeShape = newValue;
                _VolumeShape = value;
                OnPropertyChanged(nameof(VolumeShape));

                SetDBActionForChange();
            }

        }

        private IShape2D _MosaicShape;
        public IShape2D MosaicShape
        {
            get => _MosaicShape;
            set
            {
                Debug.Assert(value != null);
                if (value == null)
                    return;

                //DbGeometry newValue = value.ToDbGeometry();
                if (MosaicShape != null && MosaicShape.Equals(value)) return;

                OnPropertyChanging(nameof(MosaicShape));

                OnPropertyChanging(nameof(Position));
                if (value is ICentroid c)
                    _MosaicPosition = c.Centroid.ToGridVector2();
                else
                    _MosaicPosition = value.BoundingBox.Center;
                OnPropertyChanged(nameof(Position));

                //Data.MosaicShape = newValue;
                _MosaicShape = value;
                OnPropertyChanged(nameof(MosaicShape));

                OnPropertyChanging(nameof(Radius));
                _Radius = CalculateRadius(value);
                OnPropertyChanged(nameof(Radius));

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

        private double CalculateRadius(IShape2D shape)
        {
            if (shape is ICircle2D circle)
                return circle.Radius;

            if (shape is IRectangle rect)
                return Math.Sqrt(rect.Area);

            if (shape is ILineSegment2D line)
                return GridVector2.Distance(line.A, line.B) / 2.0;

            if (shape is IPoint2D point)
                return 8;

            if (shape is IPolygon2D poly)
                return Math.Sqrt(poly.Area);

            if (shape is IPolyLine2D polyline)
                return polyline.Length;

            return Math.Sqrt(shape.BoundingBox.Area);
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
        }

        private const double g_MinimumWidth = 1.0;

        private double? _Width;
        public double? Width
        {
            get
            {
                if (_Width.HasValue)
                    return _Width.Value < g_MinimumWidth ? g_MinimumWidth : _Width.Value;
                else
                    return _Width ?? g_MinimumWidth;
            }
            set
            {
                if (_Width.Equals(value))
                    return;

                OnPropertyChanging(nameof(Width));
                _Width = value;
                OnPropertyChanged(nameof(Width));

                SetDBActionForChange();
            }
        }

        private LocationType _TypeCode;
        public LocationType TypeCode
        {
            get => _TypeCode;
            set
            {
                if (_TypeCode == value)
                    return;

                OnPropertyChanging(nameof(TypeCode));
                _TypeCode = value;
                SetDBActionForChange();
                OnPropertyChanged(nameof(TypeCode));
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
                if (Links.Count >= 2)
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
        public long Section
        {
            get; internal set;
        }

        /// <summary>
        /// Name of the last user to edit the location.  Updated by server only.
        /// </summary>
        public string Username
        {
            get; internal set;
        }

        private ConcurrentObservableSet<long> _Links { get; set; }

        public Task<long[]> CopyLinksAsync()
        {
            return _Links.CreateCopyAsync(); 
        } 

        /// <summary>
        /// This needs sorting out.  Do we need this as an observable collection or should 
        /// we fire our own collection changed events with Add/Remove link calls.
        /// </summary>
        public ReadOnlyObservableCollection<long> Links => _Links.ReadOnlyObservable;

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        public Task<bool> AddLinkAsync(long ID)
        {
            if (ID == this.ID)
                throw new ArgumentException("Can't add own ID from location links");

            return _Links.AddAsync(ID); 
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// </summary>
        /// <param name="ID"></param>
        public Task<bool> RemoveLinkAsync(long ID)
        {
            if (ID == this.ID)
                throw new ArgumentException("Can't remove own ID from location links");

            return _Links.RemoveAsync(ID);
        }

        private bool _Terminal;
        public bool Terminal
        {
            get => _Terminal;
            set
            {
                if (_Terminal == value)
                    return;

                OnPropertyChanging(nameof(Terminal));
                _Terminal = value;
                SetDBActionForChange();
                OnPropertyChanged(nameof(Terminal));
            }
        }

        private bool _OffEdge;
        public bool OffEdge
        {
            get => _OffEdge;
            set
            {
                if (_OffEdge == value)
                    return;

                OnPropertyChanging(nameof(OffEdge));
                _OffEdge = value;
                SetDBActionForChange();
                OnPropertyChanged(nameof(OffEdge));
            }
        }

        public bool VericosityCap
        {
            get { return Attributes.Any(a => a.Name == "Varicosity Cap"); }
        }

        public bool Untraceable
        {
            get { return Attributes.Any(a => a.Name == "Untraceable"); }
        }

        private DateTime _LastModified;

        /// <summary>
        /// Only the server can update this attribute.  Protected set is to allow factory function to set the value.
        /// </summary>
        public DateTime LastModified
        {
            get => _LastModified;
            internal set { 
                if(_LastModified != value)
                {
                    OnPropertyChanging(nameof(LastModified));
                    _LastModified = value; 
                    //Do not set DB action to changed because the server generates this
                    OnPropertyChanged(nameof(LastModified));
                }
            }
        }

        private ConcurrentObservableAttributeSet _Attributes { get; set; }
        public ReadOnlyObservableCollection<ObjAttribute> Attributes => _Attributes.ReadOnlyObservable;

        ulong ILocationReadOnly.ID => (ulong)ID;

        ulong ILocationReadOnly.ParentID => (ulong)ParentID;

        bool ILocationReadOnly.IsVericosityCap => VericosityCap;

        bool ILocationReadOnly.IsUntraceable => Untraceable;

        IReadOnlyDictionary<string, string> ILocationReadOnly.Attributes =>
            _Attributes.ReadOnlyObservable.ToDictionary(o => o.Name, o => o.Value);

        long ILocationReadOnly.UnscaledZ => (long)Z;

        string ILocationReadOnly.MosaicGeometryWKT => MosaicShape?.ToWKT() ?? null;

        string ILocationReadOnly.VolumeGeometryWKT => VolumeShape?.ToWKT() ?? null;

        public Task<ObjAttribute[]> CopyAttributesAsync()
        {
            return _Attributes.CreateCopyAsync();
        }
         
        internal Task SetAttributes(IEnumerable<ObjAttribute> attribs)
        {
            return _Attributes.SetAttributes(attribs);
        }

        /// <summary>
        /// Add the specified name to the attributes if it does not exists, removes it 
        /// </summary>
        /// <param name="tag"></param>
        public Task<bool> ToggleAttribute(string tag, string value = null)
        {
            return _Attributes.ToggleAttribute(tag, value);
        }

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        public Task AddAttributeAsync(ObjAttribute attribute)
        {
            return _Attributes.AddAsync(attribute);
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// </summary>
        /// <param name="ID"></param>
        public Task RemoveAttributeAsync(ObjAttribute attribute)
        {
            return _Attributes.RemoveAsync(attribute);
        }

        public LocationObj(long id)
        {
            _ID = id;
        }

        public LocationObj(long id, long parentid)
        {
            _ID = id;
            ParentID = parentid;
        }

        public LocationObj(StructureObj parent,
                           int SectionNumber, LocationType shapeType)
        {
            this.DBAction = DBACTION.INSERT;
            //this._ID = Store.Locations.NextKey();
            this.TypeCode = shapeType;

            if (shapeType == LocationType.CIRCLE)
                this._Radius = 16;

            if (shapeType == LocationType.POINT)
                this._Radius = 16;
              
            this.Section = SectionNumber;

            if (parent != null)
            {
                this.ParentID = parent.ID;
            } 
        }

        public LocationObj(StructureObj parent,
                           IShape2D mosaicShape, IShape2D volumeShape,
                           int SectionNumber, LocationType shapeType) : this(parent, SectionNumber, shapeType)
        {
            //this.Data.MosaicShape = mosaicShape.ToDbGeometry();
            //this.Data.VolumeShape = volumeShape.ToDbGeometry();

            this._VolumeShape = mosaicShape;
            this._MosaicShape = volumeShape; 
        }

        /// <summary>
        /// Creates an instance but does not send change events
        /// </summary>
        /// <param name="newData"></param>
        /// <returns></returns>
        internal static async Task<LocationObj> CreateFromServerAsync(ILocation newData)
        {
            LocationObj obj = new LocationObj(newData.ID)
            {
                ParentID = newData.ParentID,
                _Attributes = new ConcurrentObservableAttributeSet(ObjAttributeParser.ParseAttributes(newData.Attributes)),
                _DBAction = DBACTION.NONE,
                Width = newData.Width,
                _Radius = newData.Radius,
                _MosaicPosition = newData.MosaicPosition.XY(),
                _VolumePosition = newData.VolumePosition.XY(),
                Username = newData.Username,
                OffEdge = newData.OffEdge,
                Terminal = newData.Terminal,
                Section = newData.SectionNumber,
                _Links = new ConcurrentObservableSet<long>(newData.Links)
            };

            foreach (long link in newData.Links)
            {
                await obj._Links.AddAsync(link);
            }

            Debug.Assert(false, "Unfinished LocationObj creation");

            return obj;
        }

        /// <summary>
        /// Override and write each property individually so we send specific property changed events
        /// </summary>
        /// <param name="newdata"></param>
        internal override async Task Update(ILocation newdata)
        {
            Debug.Assert(this.ID == newdata.ID);
            this.DBAction = Viking.AnnotationServiceTypes.Interfaces.DBACTION.NONE;
            this.TypeCode = newdata.TypeCode;
            this._Radius = newdata.Radius;
            this.Section = newdata.SectionNumber;
            this.Terminal = newdata.Terminal;
            this.OffEdge = newdata.OffEdge;
            this.ParentID = newdata.ParentID;
            this.Username = newdata.Username;
            this.LastModified = newdata.LastModified;
            await _Attributes.ClearAsync();
            foreach (var a in ObjAttributeParser.ParseAttributes(newdata.Attributes))
            {
                await _Attributes.AddAsync(a);
            }

            this.VolumeShape = newdata.VolumeGeometryWKT.ToShape2D();
            this.MosaicShape = newdata.MosaicGeometryWKT.ToShape2D();
            await this._Links.ClearAsync();
            foreach (long link in newdata.Links)
            {
                await AddLinkAsync(link);
            }

            return;
        }
         
        protected static event EventHandler OnCreate;
        protected void CallOnCreate()
        {
            if (OnCreate != null)
            {
                //Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, new object[] { this, null });
                OnCreate(this, null);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is LocationObj other)
                return Equals(other);

            return base.Equals(obj);
        }

        public bool Equals(LocationObj other)
        {
            if (other is null)
                return false;

            return ID.Equals(other.ID);
        }

        bool IEquatable<ILocationReadOnly>.Equals(ILocationReadOnly other)
        {
            throw new NotImplementedException();
        }

        /*
        public static event EventHandler Create
        {
            add { OnCreate += value; }
            remove { OnCreate -= value; }
        }*/
    }
}
