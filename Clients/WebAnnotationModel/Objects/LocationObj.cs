using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using WebAnnotationModel;
using WebAnnotationModel.Objects; 
using WebAnnotationModel.Service;
using System.Data.Entity.Spatial;
using SqlGeometryUtils;

using Geometry; 


namespace WebAnnotationModel
{
    public enum LocationType
    {
        POINT = 0,
        CIRCLE = 1,
        ELLIPSE = 2,
        POLYLINE = 3,
        POLYGON = 4,
        OPENCURVE = 5,
        CLOSEDCURVE = 6
    };

    public class LocationObj : WCFObjBaseWithKey<long, Location>
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
                case "VolumePosition":
                    return true;
                case "VolumeShape":
                    return true;
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
                case "VolumeShape":
                    return true;
                case "MosaicShape":
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
                    _MosaicPosition = this.MosaicShape.Centroid();
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
                    _VolumePosition = Data.VolumeShape.Centroid();
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

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        public double Z
        {
            get {return Data.Position.Z; }
        }

        private Microsoft.SqlServer.Types.SqlGeometry _VolumeShape;
        public Microsoft.SqlServer.Types.SqlGeometry VolumeShape
        {
            get
            {
                if (_VolumeShape == null)
                {
                    _VolumeShape = Data.VolumeShape.ToSqlGeometry();
                }
                return _VolumeShape;
            }
            set
            {
                Debug.Assert(value != null);
                if (value == null)
                    return;

                DbGeometry newValue = value.ToDbGeometry();
                if (Data.VolumeShape != null && Data.VolumeShape.SpatialEquals(newValue)) return;

                OnPropertyChanging("VolumeShape");

                OnPropertyChanging("VolumePosition");
                _VolumePosition = value.Centroid();
                OnPropertyChanged("VolumePosition");

                Data.VolumeShape = newValue;
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
                if (_MosaicShape == null)
                {
                    _MosaicShape = Data.MosaicShape.ToSqlGeometry();
                }
                return _MosaicShape;
            }
            set
            {
                Debug.Assert(value != null);
                if (value == null)
                    return;

                DbGeometry newValue = value.ToDbGeometry();
                if (Data.MosaicShape != null && Data.MosaicShape.SpatialEquals(newValue)) return;

                OnPropertyChanging("MosaicShape");

                OnPropertyChanging("Position");
                _MosaicPosition = value.Centroid();
                OnPropertyChanged("Position");

                Data.MosaicShape = newValue;
                _MosaicShape = null;
                OnPropertyChanged("MosaicShape");

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
        
        private const double g_MinimumRadius = 1.0;
        public double Radius
        {
            get {
                if (Data.Radius < g_MinimumRadius)
                {
                    return g_MinimumRadius;
                }
                return Data.Radius; }
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
        }

        public LocationType TypeCode
        {
            get { return (LocationType)Data.TypeCode; }
            set {
            if(Data.TypeCode == (short)value)
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
                return !(Terminal || OffEdge || VericosityCap || Untraceable );
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

        private object LinkLock = new object();

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
            get {
                lock (LinkLock)
                {
                    if(_ObservableLinks == null)
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

        public bool Terminal
        {
            get { return Data.Terminal; }
            set {
                if (Data.Terminal == value)
                    return;

                OnPropertyChanging("Terminal"); 
                Data.Terminal = value;
                SetDBActionForChange();
                OnPropertyChanged("Terminal"); 
            }
        }

        public bool OffEdge
        {
            get { return Data.OffEdge; }
            set {
                if (Data.OffEdge == value)
                    return;

                OnPropertyChanging("OffEdge"); 
                Data.OffEdge = value;
                SetDBActionForChange();
                OnPropertyChanged("OffEdge"); 
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

        public DateTime LastModified
        {
            get { return new DateTime(Data.LastModified, DateTimeKind.Utc); }
        }

        List<ObjAttribute> _Attributes = null;

        public IEnumerable<ObjAttribute> Attributes
        {
            get {

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

        /// <summary>
        /// Add the specified name to the attributes if it does not exists, removes it 
        /// </summary>
        /// <param name="tag"></param>
        public void ToggleAttribute(string tag)
        {
            ObjAttribute attrib = new ObjAttribute(tag, null);
            List<ObjAttribute> listAttributes = this.Attributes.ToList();
            if (listAttributes.Contains(attrib))
            {
                listAttributes.Remove(attrib);
            }
            else
            {
                listAttributes.Add(attrib);
            }

            this.Attributes = listAttributes;
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
            this.Data = new Location();
            this.Data.DBAction = DBACTION.INSERT;
            this.Data.ID = Store.Locations.GetTempKey();
            this.Data.TypeCode = (short)shapeType;
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
            this.Data.MosaicShape = mosaicShape.ToDbGeometry();
            this.Data.VolumeShape = volumeShape.ToDbGeometry();
        }

        /// <summary>
        /// Override and write each property individually so we send specific property changed events
        /// </summary>
        /// <param name="newdata"></param>
        internal override void Update(Location newdata)
        {
            Debug.Assert(this.Data.ID == newdata.ID);
            this.Data.DBAction = DBACTION.NONE;
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
            this.Data.VolumeShape = newdata.VolumeShape;
            this.Data.MosaicShape = newdata.MosaicShape;
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
            if (OnCreate != null)
            {
                //Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, new object[] { this, null });
                OnCreate(this, null); 
            }
        }

        public static event EventHandler Create
        {
            add { OnCreate += value; }
            remove { OnCreate -= value; }
        }
    }
}
