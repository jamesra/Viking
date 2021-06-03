using ProtoBuf;
using System;


namespace Viking.gRPC.AnnotationTypes
{

    /// <summary>
    /// A generic database object that exposes an ID value and Parent of
    /// the same type referring to a row in the same table
    /// </summary>
    /* Recoded [DataContract] */
    [ProtoContract]
    [ProtoInclude(1, typeof(Structure))]
    [ProtoInclude(2, typeof(StructureType))]
    public class DataObjectWithParentOfLong : DataObjectWithKeyOfLong
    {
        private Int64? _ParentID;

        [ProtoMember(10)]
        /* Recoded [DataMember] */
        public Int64? ParentID
        {
            get
            {
                if (_ParentID.HasValue)
                    return _ParentID;
                else
                {
                    return new Int64?();
                }
            }
            set { _ParentID = value; }
        }
    }
}

