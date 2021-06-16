//using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Viking.AnnotationServiceTypes;

namespace Viking.AnnotationServiceTypes.gRPC
{


    [ProtoContract]
    /* Recoded [DataContract] */
    [ProtoInclude(1000, typeof(LocationHistory))]
    public partial class Location : IDataObjectWithKey<Int64>
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
        private AnnotationType _TypeCode;
        private Int64 _LastModified;
        private string _Username;
        private string _Xml;
        private Geometry _MosaicShape;
        private Geometry _VolumeShape;
        private byte[] _MosaicShapeWKB;
        private byte[] _VolumeShapeWKB;

        [ProtoMember(1)]
        /* Recoded [DataMember] */
        public Int64 ParentID
        {
            get { return _ParentID; }
            set { _ParentID = value; }
        }

        [ProtoMember(2)]
        /* Recoded [DataMember] */
        public Int64 Section
        {
            get { return _Section; }
            set
            {
                _Section = value;
                this._Position.Z = (double)value;
            }
        }

        [ProtoMember(3)]
        /* Recoded [DataMember] */
        public AnnotationPoint Position
        {
            get { return _Position; }
            set { _Position = value; }
        }

        [ProtoMember(4)]
        /* Recoded [DataMember] */
        public AnnotationPoint VolumePosition
        {
            get { return _VolumePosition; }
            set { _VolumePosition = value; }
        }

        //[ProtoMember(5)]
        //[DataMember]
        public  Geometry MosaicShape
        {
            get
            {
                if (_MosaicShape == null && _MosaicShapeWKB != null)
                {
                    throw new NotImplementedException();
                    //_MosaicShape = Geometry.DefaultFactory.CreatePolygon.(_MosaicShapeWKB);
                }
                return _MosaicShape;
            }
            //set { _MosaicShape = value; }
        }

        // [ProtoMember(6)]
        //[DataMember]
        public Geometry VolumeShape
        {
            get
            {
                if (_VolumeShape == null && _VolumeShapeWKB != null)
                {
                    throw new NotImplementedException();
                    //_VolumeShape = Geometry.FromBinary(_VolumeShapeWKB);
                }
                return _VolumeShape;
            }
            //set { _VolumeShape = value; }

        }

        [ProtoMember(7)]
        /* Recoded [DataMember] */
        public byte[] MosaicShapeWKB
        {
            get { return _MosaicShapeWKB; }
            set
            {
                _MosaicShapeWKB = value;
                _MosaicShape = null;
            }
        }

        [ProtoMember(8)]
        /* Recoded [DataMember] */
        public byte[] VolumeShapeWKB
        {
            get { return _VolumeShapeWKB; }
            set
            {
                _VolumeShapeWKB = value;
                _VolumeShape = null;
            }
        }

        [ProtoMember(9)]
        /* Recoded [DataMember] */
        //[Column("Closed")]
        public bool Closed
        {
            get { return _Closed; }
            set { _Closed = value; }
        }

        [ProtoMember(10)]
        /* Recoded [DataMember] */
        public string AttributesXml
        {
            get { return _Xml; }
            set { _Xml = value; }
        }

        [ProtoMember(11)]
        /* Recoded [DataMember] */
        //[Column("Links")]
        public Int64[] Links
        {
            get
            {
                if (_Links == null)
                    return null;
                if (_Links.Count == 0)
                    return null;
                else
                    return _Links.ToArray();
            }
            set
            {
                if (value == null)
                {
                    _Links = null;
                }
                else
                {
                    _Links = new SortedSet<Int64>(value);
                }
            }
        }

        [ProtoMember(12)]
        /* Recoded [DataMember] */
        //[Column("Extensible")]
        public bool Terminal
        {
            get { return _Terminal; }
            set { _Terminal = value; }
        }

        [ProtoMember(13)]
        /* Recoded [DataMember] */
        //[Column("OffEdge")]
        public bool OffEdge
        {
            get { return _OffEdge; }
            set { _OffEdge = value; }
        }

        [ProtoMember(14)]
        /* Recoded [DataMember] */
        //[Column("Radius")]
        public double Radius
        {
            get { return _Radius; }
            set { _Radius = value; }
        }

        [ProtoMember(15)]
        /* Recoded [DataMember] */
       // [Column("Width")]
        public double? Width
        {
            get { return _Width; }
            set { _Width = value; }
        }

        [ProtoMember(16)]
        /* Recoded [DataMember] */
       // [Column("TypeCode")]
        public AnnotationType TypeCode
        {
            get { return _TypeCode; }
            set { _TypeCode = value; }
        }

        [ProtoMember(17)]
        /* Recoded [DataMember] */
       // [Column("LastModified")]
        public Int64 LastModified
        {
            get { return _LastModified; }
            set { _LastModified = value; }
        }

        [ProtoMember(18)]
        /* Recoded [DataMember] */
       // [Column("Username")]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }

        public void AddLink(Int64 linkedID)
        {
            if (this._Links == null)
                _Links = new SortedSet<Int64>();
            if (linkedID == this.ID)
            {
                throw new ArgumentException("Cannot link location to itself: ID = " + this.ID.ToString());
            }

            _Links.Add(linkedID);
        }

        public void AddLinks(SortedSet<Int64> linkIDs)
        {
            if (this._Links == null)
                _Links = new SortedSet<Int64>();

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

        public static Int64 MeasureProtobufEncodedObjectSize(Location loc)
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
}

