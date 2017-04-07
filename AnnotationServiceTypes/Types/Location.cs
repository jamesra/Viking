using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization; 
using ProtoBuf;
using Annotation;

namespace AnnotationService.Types
{
    [ProtoContract]
    [DataContract]
    public class LocationPositionOnly : DataObjectWithKey<long>
    {
        AnnotationPoint _Position;
        private double _Radius;

        [ProtoMember(1)]
        [DataMember]
        public AnnotationPoint Position
        {
            get { return _Position; }
            set { _Position = value; }
        }

        [ProtoMember(2)]
        [DataMember]
        [Column("Radius")]
        public double Radius
        {
            get { return _Radius; }
            set { _Radius = value; }
        }

        
    }


    [ProtoContract]
    [DataContract]
    [ProtoInclude(1000, typeof(LocationHistory))]
    public class Location : DataObjectWithKey<long>
    {
        protected long _ParentID;
        protected long _Section;
        protected AnnotationPoint _Position;
        protected AnnotationPoint _VolumePosition;
        protected bool _Closed;
        protected SortedSet<long> _Links = null;
        protected bool _Terminal;
        protected bool _OffEdge;
        protected double _Radius;
        protected double? _Width;
        protected short _TypeCode;
        protected long _LastModified;
        protected string _Username;
        protected string _Xml;
        protected System.Data.Entity.Spatial.DbGeometry _MosaicShape;
        protected System.Data.Entity.Spatial.DbGeometry _VolumeShape;

        [ProtoMember(1)]
        [DataMember]
        public long ParentID
        {
            get { return _ParentID; }
            set { _ParentID = value; }
        }

        [ProtoMember(2)]
        [DataMember]
        public long Section
        {
            get { return _Section; }
            set { _Section = value;
                  this._Position.Z = (double)value; 
            }
        }

        [ProtoMember(3)]
        [DataMember]
        public AnnotationPoint Position
        {
            get { return _Position; }
            set { _Position = value; }
        }

        [ProtoMember(4)]
        [DataMember]
        public AnnotationPoint VolumePosition
        {
            get { return _VolumePosition; }
            set { _VolumePosition = value; }
        }

        [ProtoMember(5)]
        [DataMember]
        public System.Data.Entity.Spatial.DbGeometry MosaicShape
        {
            get { return _MosaicShape; }
            set { _MosaicShape = value; }
        }

        [ProtoMember(6)]
        [DataMember]
        public System.Data.Entity.Spatial.DbGeometry VolumeShape
        {
            get { return _VolumeShape; }
            set { _VolumeShape = value; }
        }

        [ProtoMember(9)]
        [DataMember]
        [Column("Closed")]
        public bool Closed
        {
            get { return _Closed; }
            set { _Closed = value; }
        }

        [ProtoMember(10)]
        [DataMember]
        public string AttributesXml
        {
            get { return _Xml; }
            set { _Xml = value; }
        }

        [ProtoMember(11)]
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
                    _Links = null;
                }
                else
                {
                    _Links = new SortedSet<long>(value);
                } 
            }
        }

        [ProtoMember(12)]
        [DataMember]
        [Column("Extensible")]
        public bool Terminal
        {
            get { return _Terminal; }
            set { _Terminal = value; }
        }

        [ProtoMember(13)]
        [DataMember]
        [Column("OffEdge")]
        public bool OffEdge
        {
            get { return _OffEdge; }
            set { _OffEdge = value; }
        }

        [ProtoMember(14)]
        [DataMember]
        [Column("Radius")]
        public double Radius
        {
            get { return _Radius; }
            set { _Radius = value; }
        }

        [ProtoMember(15)]
        [DataMember]
        [Column("Width")]
        public double? Width
        {
            get { return _Width; }
            set { _Width = value; }
        }

        [ProtoMember(16)]
        [DataMember]
        [Column("TypeCode")]
        public short TypeCode
        {
            get { return _TypeCode; }
            set { _TypeCode = value; }
        }

        [ProtoMember(17)]
        [DataMember]
        [Column("LastModified")]
        public long LastModified
        {
            get { return _LastModified; }
            set { _LastModified = value; }
        }

        [ProtoMember(18)]
        [DataMember]
        [Column("Username")]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }

        public void AddLink(long linkedID)
        {
            if (this._Links == null)
                _Links = new SortedSet<long>();
            if(linkedID == this.ID)
            {
                throw new ArgumentException("Cannot link location to itself: ID = " + this.ID.ToString());
            }

            _Links.Add(linkedID);
        }

        public void AddLinks(SortedSet<long> linkIDs)
        {
            if (this._Links == null)
                _Links = new SortedSet<long>();

            if (linkIDs.Contains(this.ID))
            {
                throw new ArgumentException("Cannot link location to itself: ID = " + this.ID.ToString());
            }

            _Links.UnionWith(linkIDs);
        }

        public Location()
        {

        }


         
        public static long MeasureEncodedObjectSize(Location loc)
        {
            DataContractSerializer ds = new DataContractSerializer(loc.GetType());

            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                ds.WriteObject(ms, loc);
                // Spit out

                string payload = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                System.Diagnostics.Trace.WriteLine("Output: " + payload);
                System.Diagnostics.Trace.WriteLine("Loc #" + loc.ID.ToString() + " Message length: " + ms.Length.ToString());

                return ms.Length;
            }
        }

        public static long MeasureProtobufEncodedObjectSize(Location loc)
        {  
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                Serializer.Serialize(ms, loc);
                //ds.WriteObject(ms, loc);
                // Spit out

                string payload = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                System.Diagnostics.Trace.WriteLine("PB Output: " + payload);
                System.Diagnostics.Trace.WriteLine("PB Loc #" + loc.ID.ToString() + " Message length: " + ms.Length.ToString());

                return ms.Length;
            }
        }

        public static Location VerifyProtobufEncodedObject(Location loc)
        {
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                Serializer.Serialize(ms, loc);
                //ds.WriteObject(ms, loc);
                // Spit out

                string payload = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                System.Diagnostics.Trace.WriteLine("PB Output: " + payload);
                System.Diagnostics.Trace.WriteLine("PB Loc #" + loc.ID.ToString() + " Message length: " + ms.Length.ToString());

                Location output = Serializer.Deserialize<Location>(ms);

                return output;
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
    }
}
