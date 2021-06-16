using ProtoBuf;
using System;


namespace Viking.AnnotationServiceTypes.gRPC
{

    /// <summary>
    /// A generic database object that exposes a key value
    /// </summary>
    /* Recoded [DataContract] */
    [ProtoContract]
    [ProtoInclude(1, typeof(DataObjectWithParentOfLong))]
    [ProtoInclude(2, typeof(Location))]
    [ProtoInclude(3, typeof(LocationPositionOnly))]
    public class DataObjectWithKeyOfLong : DataObject
    {
        private Int64 _ID;

        [ProtoMember(10)]
        /* Recoded [DataMember] */
        public Int64 ID
        {
            get { return _ID; }
            set { _ID = value; }
        }
    }
}

