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

using Geometry; 


namespace WebAnnotationModel
{
    public enum LocationType
    {
        POINT = 0,
        CIRCLE = 1,
        ELLIPSE = 2,
        POLYLINE = 3,
        POLYGON = 4
    };

    public class LocationObj : WCFObjBaseWithKey<long, Location>
    {
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

        public GridVector2 Position
        {
            get
            {
                return new GridVector2(Data.Position.X, Data.Position.Y);
            }
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
                OnPropertyChanged("Position");

                SetDBActionForChange();
            }

        }

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        public double Z
        {
            get {return Data.Position.Z; }
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
                if(!_VolumePosition.HasValue)
                    _VolumePosition = new GridVector2(Data.VolumePosition.X, Data.VolumePosition.Y);

                return _VolumePosition.Value; 
            }
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
            }
        }

        /// <summary>
        /// Record the hashcode of the volume transform used to map the location. 
        /// </summary>
        public int? VolumeTransformID = new int?();

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
                return !(Terminal || OffEdge);
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

        public DateTime LastModified
        {
            get { return new DateTime(Data.LastModified, DateTimeKind.Utc); }
        }

        public IEnumerable<ObjAttribute> Attributes
        {
            get { return ObjAttribute.Parse(Data.AttributesXml); }
            set
            {
                if (Data.AttributesXml == null && value == null)
                    return;

                string xmlstring = ObjAttribute.ToXml(value);

                if (Data.AttributesXml != xmlstring)
                {
                    OnPropertyChanging("Attributes");

                    Data.AttributesXml = xmlstring;

                    //Refresh the tags
                    SetDBActionForChange();
                    OnPropertyChanged("Attributes");
                }
            }
        }

        public LocationObj()
        {
            Data = new Location();
        }

        public LocationObj(Location obj)
        {
            Data = obj;    
        }

        public LocationObj(StructureObj parent, GridVector2 position, GridVector2 volumePosition, int SectionNumber)
        {
            this.Data = new Location();
            this.Data.DBAction = DBACTION.INSERT;
            this.Data.ID = Store.Locations.GetTempKey(); 
            this.Data.Verticies = new AnnotationPoint[0];
            this.Data.TypeCode = 1;
            this.Data.Radius = 16;
            this.Data.Links = null; 

            AnnotationPoint P = new AnnotationPoint();
            P.X = position.X;
            P.Y = position.Y;
            P.Z = (double)SectionNumber;

            AnnotationPoint VP = new AnnotationPoint();
            VP.X = volumePosition.X;
            VP.Y = volumePosition.Y;
            VP.Z = (double)SectionNumber;

            this.Data.Section = SectionNumber; 

            this.Data.Position = P;
            this.Data.VolumePosition = VP; 

            if (parent != null)
            {
                this.Data.ParentID = parent.ID;
            }

//          CallOnCreate(); 
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
            this.Data.Verticies = newdata.Verticies;
            this.Data.Username = newdata.Username;
            this.Data.LastModified = newdata.LastModified;
            this.Data.Links = newdata.Links;  
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
