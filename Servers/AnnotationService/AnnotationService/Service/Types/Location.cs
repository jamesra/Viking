using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using ConnectomeDataModel;

namespace Annotation
{
    
    [DataContract]
    [Serializable]
    public struct AnnotationPoint 
    {
        private double _X;
        private double _Y;
        private double _Z; 

        [DataMember]
        public double X
        {
            get { return _X; }
            set { _X = value; }
        }

        [DataMember]
        public double Y
        {
            get {return _Y; }
            set { _Y = value; }
        }

        [DataMember]
        public double Z
        {
            get { return _Z; }
            set { _Z = value; }
        }

        public AnnotationPoint(double x, double y, double z)
        {
            _X = x;
            _Y = y;
            _Z = z; 
        } 
    }


    [DataContract]
    [Serializable]
    public struct BoundingRectangle
    {
        private double _XMin;
        private double _YMin;
        private double _XMax;
        private double _YMax;

        [DataMember]
        public double XMin
        {
            get { return _XMin; }
            set { _XMin = value; }
        }

        [DataMember]
        public double YMin
        {
            get { return _YMin; }
            set { _YMin = value; }
        }
        
        [DataMember]
        public double XMax
        {
            get { return _XMax; }
            set { _XMax = value; }
        }

        [DataMember]
        public double YMax
        {
            get { return _YMax; }
            set { _YMax = value; }
        }
        

        public BoundingRectangle(double xmin, double ymin, double xmax, double ymax)
        {
            _XMin = xmin;
            _YMin = ymin;
            _XMax = xmax;
            _YMax = ymax;
        }
        public System.Data.Entity.Spatial.DbGeometry ToGeometry()
        {
            return System.Data.Entity.Spatial.DbGeometry.FromText(string.Format("POLYGON (( {0} {2}, {0} {3}, {1} {3}, {1} {2}, {0} {2}))", XMin, XMax, YMin, YMax));
        }
    }

    [DataContract]
    [Serializable]
    public struct BoundingBox
    {
        private double _XMin;
        private double _YMin;
        private double _ZMin;
        private double _XMax;
        private double _YMax;
        private double _ZMax;

        [DataMember]
        public double XMin
        {
            get { return _XMin; }
            set { _XMin = value; }
        }

        [DataMember]
        public double YMin
        {
            get { return _YMin; }
            set { _YMin = value; }
        }

        [DataMember]
        public double ZMin
        {
            get { return _ZMin; }
            set { _ZMin = value; }
        }

        [DataMember]
        public double XMax
        {
            get { return _XMax; }
            set { _XMax = value; }
        }

        [DataMember]
        public double YMax
        {
            get { return _YMax; }
            set { _YMax = value; }
        }

        [DataMember]
        public double ZMax
        {
            get { return _ZMax; }
            set { _ZMax = value; }
        }

        public BoundingBox(double xmin, double ymin, double zmin, double xmax, double ymax, double zmax)
        {
            _XMin = xmin;
            _YMin = ymin;
            _ZMin = zmin;
            _XMax = xmax;
            _YMax = ymax;
            _ZMax = zmax;
        } 
        public System.Data.Entity.Spatial.DbGeometry ToGeometry()
        {
            return System.Data.Entity.Spatial.DbGeometry.FromText(string.Format("POLYGON (( {0} {2}, {0} {3}, {1} {3}, {1} {2}, {0} {2}))", XMin, XMax, YMin, YMax));
        }
    }

    [DataContract]
    public class LocationPositionOnly : DataObjectWithKey<long>
    {
        AnnotationPoint _Position;
        private double _Radius;

        [DataMember]
        public AnnotationPoint Position
        {
            get { return _Position; }
            set { _Position = value; }
        }

        [DataMember]
        [Column("Radius")]
        public double Radius
        {
            get { return _Radius; }
            set { _Radius = value; }
        }

        public LocationPositionOnly(ConnectomeDataModel.SelectUnfinishedStructureBranchesWithPosition_Result db)
        {
            this.ID = db.ID;
            this.Position = new AnnotationPoint(db.X, db.Y, db.Z);
            this.Radius = db.Radius;
        }

        public LocationPositionOnly(ConnectomeDataModel.Location db)
        {
            this.ID = db.ID;
            this.Position = new AnnotationPoint(db.X, db.Y, db.Z);
            this.Radius = db.Radius;
        } 
    }



    [DataContract]
    public class Location : DataObjectWithKey<long>
    {
        protected long _ParentID;
        protected long _Section;
        protected AnnotationPoint _Position;
        protected AnnotationPoint _VolumePosition;
        protected bool _Closed;
        protected List<long> _Links = new List<long>();
        protected bool _Terminal;
        protected bool _OffEdge;
        protected double _Radius;
        protected short _TypeCode;
        protected long _LastModified;
        protected string _Username;
        protected string _Xml;
        protected System.Data.Entity.Spatial.DbGeometry _MosaicShape;
        protected System.Data.Entity.Spatial.DbGeometry _VolumeShape;

        [DataMember]
        public long ParentID
        {
            get { return _ParentID; }
            set { _ParentID = value; }
        }

        [DataMember]
        public long Section
        {
            get { return _Section; }
            set { _Section = value;
                  this._Position.Z = (double)value; 
            }
        }

        [DataMember]
        public AnnotationPoint Position
        {
            get { return _Position; }
            set { _Position = value; }
        }

        [DataMember]
        public AnnotationPoint VolumePosition
        {
            get { return _VolumePosition; }
            set { _VolumePosition = value; }
        }

        [DataMember]
        public System.Data.Entity.Spatial.DbGeometry MosaicShape
        {
            get { return _MosaicShape; }
            set { _MosaicShape = value; }
        }

        [DataMember]
        public System.Data.Entity.Spatial.DbGeometry VolumeShape
        {
            get { return _VolumeShape; }
            set { _VolumeShape = value; }
        }
        [DataMember]
        [Column("Closed")]
        public bool Closed
        {
            get { return _Closed; }
            set { _Closed = value; }
        }
        
        [DataMember]
        public string AttributesXml
        {
            get { return _Xml; }
            set { _Xml = value; }
        }

        [DataMember]
        [Column("Links")]
        public long[] Links
        {
            get {
                if (_Links == null)
                    return null;  
                if(_Links.Count == 0)
                    return null;
                else 
                    return _Links.ToArray();
            }
            set
            {
                if(value == null)
                {
                    _Links = new List<long>();
                }
                else
                {
                    _Links = new List<long>(value);
                } 
            }
        }

        [DataMember]
        [Column("Extensible")]
        public bool Terminal
        {
            get { return _Terminal; }
            set { _Terminal = value; }
        }

        [DataMember]
        [Column("OffEdge")]
        public bool OffEdge
        {
            get { return _OffEdge; }
            set { _OffEdge = value; }
        }

        [DataMember]
        [Column("Radius")]
        public double Radius
        {
            get { return _Radius; }
            set { _Radius = value; }
        }

        [DataMember]
        [Column("TypeCode")]
        public short TypeCode
        {
            get { return _TypeCode; }
            set { _TypeCode = value; }
        }

        [DataMember]
        [Column("LastModified")]
        public long LastModified
        {
            get { return _LastModified; }
            set { _LastModified = value; }
        }

        [DataMember]
        [Column("Username")]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }

        public Location()
        {

        }

        /// <summary>
        /// Populates the links array using relations from the database
        /// </summary>
        /// <param name="loc"></param>
        /// <returns></returns>
        private static List<long> PopulateLinks(ConnectomeDataModel.Location loc)
        {
            if (!(loc.LocationLinksA.Any() || loc.LocationLinksB.Any()))
                return null;

            //long[] _Links = new long[loc.LocationLinksA.Count + loc.LocationLinksB.Count];
            List<long> retlist = new List<long>(loc.LocationLinksA.Count + loc.LocationLinksB.Count);

            retlist.AddRange(loc.LocationLinksA.Select(l => l.B).ToList());
            retlist.AddRange(loc.LocationLinksB.Select(l => l.A).ToList());
             
            return retlist; 
        }

        public static void PopulateLinks(Dictionary<long, Location> Locations, IList<ConnectomeDataModel.LocationLink> links)
        {
            Location A;
            Location B;
            foreach (ConnectomeDataModel.LocationLink link in links)
            {
                if (Locations.TryGetValue(link.A, out A))
                {
                    A.AddLink(link);
                }

                if (Locations.TryGetValue(link.B, out B))
                {
                    B.AddLink(link);
                }
            } 
        } 

        /// <summary>
        /// Populate the Links array using links loaded seperately from the relations in the database
        /// </summary>
        /// <param name="link"></param>
        public void AddLink(ConnectomeDataModel.LocationLink link)
        {
            if(this._Links == null)
                _Links = new List<long>();

            this._Links.Add(link.A == this.ID ? link.B : link.A);
        }

        public Location(ConnectomeDataModel.Location db, bool LoadLinks=false)
        {
            this.ID = db.ID;

            this.ParentID = db.ParentID;
             
            this.Section = (long)db.Z;  
            this.Position = new AnnotationPoint(db.X, db.Y, db.Z);
            this.VolumePosition = new AnnotationPoint(db.VolumeX, db.VolumeY, db.Z);
            this.MosaicShape = db.MosaicShape;
            this.VolumeShape = db.VolumeShape;
            this._Closed = db.Closed;
            if(LoadLinks)
                this._Links = PopulateLinks(db);

            this._Terminal = db.Terminal;
            this._OffEdge = db.OffEdge;
            this._TypeCode = db.TypeCode;
            this._Radius = db.Radius;
             
            if (db.Tags == null)
            {
                //_Tags = new string[0];
                _Xml = null;
            }
            else
            {
                //    _Tags = db.Tags.Split(';');
                _Xml = db.Tags;
            } 

            this._LastModified = db.LastModified.Ticks;
            this._Username = db.Username;
        }

        private static System.Data.Entity.Spatial.DbGeometry SetPointShape(double X, double Y, double Z)
        {
            string point_template = "POINT ({0,F2} {1,F2} {2})";
            string point_shape_string = string.Format(point_template, new object[] { X, Y, Z });
            return System.Data.Entity.Spatial.DbGeometry.FromText(point_shape_string);
        }

        private static System.Data.Entity.Spatial.DbGeometry SetCircleShape(double X, double Y, double Z, double Radius)
        {
            if (Radius == 0)
                throw new ArgumentException("Cannot create circle with a radius of zero");

            string circle_template = "CURVEPOLYGON(CIRCULARSTRING ({1,F2} {3,F2} {6}, " +
                                                                  "{0,F2} {5,F2} {6}, " +
                                                                  "{2,F2} {3,F2} {6}, " +
                                                                  "{0,F2} {4,F2} {6}, " +
                                                                  "{1,F2} {3,F2} {6}))";
            string circle_shape_string = string.Format(circle_template, new object[] {X,  X - Radius, X + Radius, Y, Y - Radius, Y + Radius, Z });
            return System.Data.Entity.Spatial.DbGeometry.FromText(circle_shape_string);
        }


        public void Sync(ConnectomeDataModel.Location db)
        {
            //This is a hack.  I want to update VolumeX and VolumeY with the viking client, but I don't want to 
            //write all the code for a server utility to update it manually.  So if the only column changing is 
            //VolumeX and VolumeY we do not update the username field.  Currently if I regenerate the volume transforms the
            //next client to run viking would plaster the username history if I did not do this.
            bool UpdateUserName = false;

            UpdateUserName |= db.ParentID != this.ParentID; 
            db.ParentID = this.ParentID;

            UpdateUserName |= db.X != this.Position.X; 
            //db.X = this.Position.X;

            UpdateUserName |= db.Y != this.Position.Y; 
            //db.Y = this.Position.Y;

            UpdateUserName |= db.Z != this.Position.Z; 
            db.Z = this.Position.Z;            

            UpdateUserName |= !db.MosaicShape.SpatialEquals(this.MosaicShape);
            if (this.MosaicShape == null)
            {
                System.Data.Entity.Spatial.DbGeometry new_geom = this.Radius > 0 ? SetCircleShape(this.Position.X, this.Position.Y, this.Position.Z, this.Radius) : SetPointShape(this.Position.X, this.Position.Y, this.Position.Z);
                UpdateUserName |= !db.MosaicShape.SpatialEquals(this.MosaicShape);
                db.MosaicShape = new_geom;
            }
            else
            {
                UpdateUserName |= !db.MosaicShape.SpatialEquals(this.MosaicShape);
                db.MosaicShape = this.MosaicShape;
            }
                
            //See above comment before adding UpdateUserName test...
            //UpdateUserName |= db.VolumeShape != this.VolumeShape;
            if (this.VolumeShape == null)
                db.VolumeShape = this.Radius > 0 ? SetCircleShape(this.VolumePosition.X, this.VolumePosition.Y, this.VolumePosition.Z, this.Radius) : SetPointShape(this.VolumePosition.X, this.VolumePosition.Y, this.VolumePosition.Z);
            else
                db.VolumeShape = this.VolumeShape;

            //See above comment before adding UpdateUserName test...
            //db.VolumeX = this.VolumePosition.X;
            //db.VolumeY = this.VolumePosition.Y;


            UpdateUserName |= db.Closed != this.Closed; 
            db.Closed = this.Closed;

            //Update the tags
            /*
            string tags = "";
            foreach (string s in _Tags)
            {
                tags = s + ';';
            }
            */
            if (db.Tags != null)
                if (db.Tags != this.AttributesXml)
                    if (!(db.Tags.Length <= 1 && this.AttributesXml.Length <= 1))
                        UpdateUserName = true;

            if (this.AttributesXml == "")
                db.Tags = null;
            else
                db.Tags = this.AttributesXml;

            UpdateUserName |= db.Terminal != this._Terminal; 
            db.Terminal = this._Terminal;

            UpdateUserName |= db.OffEdge != this._OffEdge; 
            db.OffEdge = this._OffEdge;

            UpdateUserName |= db.TypeCode != this._TypeCode; 
            db.TypeCode = this._TypeCode;

            UpdateUserName |= db.Radius != this._Radius; 
            db.Radius = this._Radius;

            UpdateUserName |= db.Username == null; 

            if (UpdateUserName)
            {
                db.Username = ServiceModelUtil.GetUserForCall();
            }
            else if (db.Username == null)
            {
                if(this.Username != null)
                    db.Username = this.Username; 
                else
                    db.Username = ServiceModelUtil.GetUserForCall();
            }

            /*  This doesn't work because we don't have the actual LocationLink objects so LINQ doesn't create the field correctly
            //Create links
            db.LinkedFrom.Clear();
            db.LinkedTo.Clear();        

            //Enforce the rule of A < B in the database for now since we don't have directionality
            for (int i = 0; i < _Links.Length; i++)
            {
                long LinkID = _Links[i];
                if (LinkID < this.ID)
                {
                    ConnectomeDataModel.LocationLink newLink = new ConnectomeDataModel.LocationLink();
                    newLink.LinkedFrom = LinkID;
                    newLink.LinkedTo = this.ID;
                    db.LinkedFrom.Add(newLink);
                }
                else
                {
                    ConnectomeDataModel.LocationLink newLink = new ConnectomeDataModel.LocationLink();
                    newLink.LinkedFrom = this.ID;
                    newLink.LinkedTo = LinkID;
                    db.LinkedTo.Add(newLink);
                }
            }
             */
        }

        /// <summary>
        /// Add the links to the locations in the dictionary
        /// </summary>
        /// <param name="Locations"></param>
        /// <param name="LocationLinks"></param>
        public static void AppendLinksToLocations(IDictionary<long, Location> Locations, IEnumerable<ConnectomeDataModel.LocationLink> LocationLinks)
        {
            Location A;
            Location B;
            foreach (ConnectomeDataModel.LocationLink link in LocationLinks)
            {
                if (Locations.TryGetValue(link.A, out A))
                {
                    A._Links.Add(link.B);
                }

                if (Locations.TryGetValue(link.B, out B))
                {
                    B._Links.Add(link.A);
                }
            }
        }
        
    }

    [DataContract]
    public class LocationHistory : Location
    {
        protected ulong  _ChangedColumnMask = 0;

        [DataMember]
        [Column("ChangedColumnMask")]
        public ulong ChangedColumnMask
        {
            get
            {
                return _ChangedColumnMask;
            }
            set
            {
                _ChangedColumnMask = value; 
            }
        }


        public LocationHistory(SelectStructureLocationChangeLog_Result db)
        {
            this.ID = db.ID.Value; 
            this.ParentID = db.ParentID.Value;

            this.Section = (long)db.Z;
            if (db.X != null && db.Y != null)
            {
                this.Position = new AnnotationPoint(db.X.Value, db.Y.Value, db.Z.Value);
            }
            else
            {
                this.Position = new AnnotationPoint(double.NaN, double.NaN, db.Z.Value);
            }

            if (db.VolumeX != null && db.VolumeY != null)
            {
                this.VolumePosition = new AnnotationPoint(db.VolumeX.Value, db.VolumeY.Value, db.Z.Value);
            }
            else
            {
                this.VolumePosition = new AnnotationPoint(double.NaN, double.NaN, db.Z.Value);
            }
             
            this._Closed = db.Closed.Value;
            this._Links = null; 
            this._Terminal = db.Terminal.Value;
            this._OffEdge = db.OffEdge.Value;
            this._TypeCode = db.TypeCode.Value;
            this._Radius = db.Radius.Value;
            this._ChangedColumnMask = 0; //TODO: System.Convert.ToUInt64(db.___update_mask); 

            
            if (db.Tags == null)
            {
                //_Tags = new string[0];
                _Xml = null;
            }
            else
            {
                _Xml = db.Tags;
            } 

            this._LastModified = db.LastModified.Value.Ticks;
            this._Username = db.Username;
        }
    }
}
