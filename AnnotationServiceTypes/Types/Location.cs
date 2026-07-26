using Annotation;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
#if NET48
using System.Data.Entity;
#endif

namespace AnnotationService.Types
{
    [ProtoContract]
    [DataContract]
    public class LocationPositionOnly : DataObjectWithKeyOfLong
    {
        AnnotationPoint _Position;
        private double _Radius;

        [ProtoMember(10)]
        [DataMember]
        public AnnotationPoint Position
        {
            get => _Position;
            set => _Position = value;
        }

        [ProtoMember(11)]
        [DataMember]
        [Column("Radius")]
        public double Radius
        {
            get => _Radius;
            set => _Radius = value;
        }
    }


    [ProtoContract]
    [DataContract]
    [ProtoInclude(1000, typeof(LocationHistory))]
    public class Location : DataObjectWithKeyOfLong
    {
        private Int64 _ParentID;
        private Int64 _Section;
        private AnnotationPoint _Position;
        private AnnotationPoint _VolumePosition;
        private bool _Closed;
        private SortedSet<Int64> _Links = null;
        private bool _Terminal;
        private bool _OffEdge;
        private double _Radius;
        private double? _Width;
        private short _TypeCode;
        private Int64 _LastModified;
        private string _Username;
        private string _Xml;
#if NET48
        private System.Data.Entity.Spatial.DbGeometry _MosaicShape;
        private System.Data.Entity.Spatial.DbGeometry _VolumeShape;
#endif
        private byte[] _MosaicShapeWKB;
        private byte[] _VolumeShapeWKB;

        [ProtoMember(10)]
        [DataMember]
        public Int64 ParentID
        {
            get => _ParentID;
            set => _ParentID = value;
        }

        [ProtoMember(11)]
        [DataMember]
        public Int64 Section
        {
            get => _Section;
            set
            {
                _Section = value;
                this._Position.Z = (double)value;
            }
        }

        [ProtoMember(12)]
        [DataMember]
        public AnnotationPoint Position
        {
            get => _Position;
            set => _Position = value;
        }

        [ProtoMember(13)]
        [DataMember]
        public AnnotationPoint VolumePosition
        {
            get => _VolumePosition;
            set => _VolumePosition = value;
        }

        //[ProtoMember(5)]
        //[DataMember]
#if NET48
        public System.Data.Entity.Spatial.DbGeometry MosaicShape
        {
            get
            {
                if (_MosaicShape is null && _MosaicShapeWKB != null)
                {
                    _MosaicShape = System.Data.Entity.Spatial.DbGeometry.FromBinary(_MosaicShapeWKB);
                }
                return _MosaicShape;
            }
            //set { _MosaicShape = value; }
        }

        // [ProtoMember(6)]
        //[DataMember]
        public System.Data.Entity.Spatial.DbGeometry VolumeShape
        {
            get
            {
                if (_VolumeShape is null && _VolumeShapeWKB != null)
                {
                    _VolumeShape = System.Data.Entity.Spatial.DbGeometry.FromBinary(_VolumeShapeWKB);
                }
                return _VolumeShape;
            }
            //set { _VolumeShape = value; }

        }
#endif

        [ProtoMember(14)]
        [DataMember]
        public byte[] MosaicShapeWKB
        {
            get => _MosaicShapeWKB;
            set
            {
                _MosaicShapeWKB = value;
#if NET48
                _MosaicShape = null;
#endif
            }
        }

        [ProtoMember(15)]
        [DataMember]
        public byte[] VolumeShapeWKB
        {
            get => _VolumeShapeWKB;
            set
            {
                _VolumeShapeWKB = value;
#if NET48
                _VolumeShape = null;
#endif
            }
        }

        [ProtoMember(16)]
        [DataMember]
        [Column("Closed")]
        public bool Closed
        {
            get => _Closed;
            set => _Closed = value;
        }

        [ProtoMember(17)]
        [DataMember]
        public string AttributesXml
        {
            get => _Xml;
            set => _Xml = value;
        }

        [ProtoMember(18)]
        [DataMember]
        [Column("Links")]
        public Int64[] Links
        {
            get
            {
                if (_Links is null)
                    return null;
                if (_Links.Count == 0)
                    return null;
                else
                    return [.. _Links];
            }
            set => _Links = value is null ? null : [.. value];
        }

        [ProtoMember(19)]
        [DataMember]
        [Column("Extensible")]
        public bool Terminal
        {
            get => _Terminal;
            set => _Terminal = value;
        }

        [ProtoMember(20)]
        [DataMember]
        [Column("OffEdge")]
        public bool OffEdge
        {
            get => _OffEdge;
            set => _OffEdge = value;
        }

        [ProtoMember(21)]
        [DataMember]
        [Column("Radius")]
        public double Radius
        {
            get => _Radius;
            set => _Radius = value;
        }

        [ProtoMember(22)]
        [DataMember]
        [Column("Width")]
        public double? Width
        {
            get => _Width;
            set => _Width = value;
        }

        [ProtoMember(23)]
        [DataMember]
        [Column("TypeCode")]
        public short TypeCode
        {
            get => _TypeCode;
            set => _TypeCode = value;
        }

        [ProtoMember(24)]
        [DataMember]
        [Column("LastModified")]
        public Int64 LastModified
        {
            get => _LastModified;
            set => _LastModified = value;
        }

        [ProtoMember(25)]
        [DataMember]
        [Column("Username")]
        public string Username
        {
            get => _Username;
            set => _Username = value;
        }

        public void AddLink(Int64 linkedID)
        {
            if (this._Links is null)
                _Links = [];
            if (linkedID == this.ID)
            {
                throw new ArgumentException("Cannot link location to itself: ID = " + this.ID.ToString());
            }

            _Links.Add(linkedID);
        }

        public void AddLinks(SortedSet<Int64> linkIDs)
        {
            if (this._Links is null)
                _Links = [];

            if (linkIDs.Contains(this.ID))
            {
                throw new ArgumentException("Cannot link location to itself: ID = " + this.ID.ToString());
            }

            _Links.UnionWith(linkIDs);
        }

        public Location()
        {

        }



        public static Int64 MeasureEncodedObjectSize(Location loc)
        {
            DataContractSerializer ds = new(loc.GetType());

            using System.IO.MemoryStream ms = new();
            ds.WriteObject(ms, loc);
            // Spit out

            string payload = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            System.Diagnostics.Trace.WriteLine("Output: " + payload);
            System.Diagnostics.Trace.WriteLine("Loc #" + loc.ID.ToString() + " Message length: " + ms.Length.ToString());

            return ms.Length;
        }

        public static Int64 MeasureProtobufEncodedObjectSize(Location loc)
        {
            using System.IO.MemoryStream ms = new();
            Serializer.Serialize(ms, loc);
            //ds.WriteObject(ms, loc);
            // Spit out

            string payload = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            System.Diagnostics.Trace.WriteLine("PB Output: " + payload);
            System.Diagnostics.Trace.WriteLine("PB Loc #" + loc.ID.ToString() + " Message length: " + ms.Length.ToString());

            return ms.Length;
        }

        public static Location VerifyProtobufEncodedObject(Location loc)
        {
            using System.IO.MemoryStream ms = new();
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

    [ProtoContract]
    [DataContract]
    public class LocationHistory : Location
    {
        private Int64 _ChangedColumnMask = 0;

        [ProtoMember(30)]
        [DataMember]
        [Column("ChangedColumnMask")]
        public Int64 ChangedColumnMask
        {
            get => _ChangedColumnMask;
            set => _ChangedColumnMask = value;
        }
    }
}
