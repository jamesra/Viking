using ProtoBuf;
using System;
using System.ComponentModel;
using System.Runtime.Serialization;


namespace AnnotationService.Types
{
    // I can't use straight inheritance because the relationships do not marshal.  So use interfaces instead

    /// <summary>
    /// A generic database object
    /// </summary>
    [DataContract]
    [ProtoContract]
    [ProtoInclude(100, typeof(LocationLink))]
    [ProtoInclude(101, typeof(StructureLink))]
    [ProtoInclude(102, typeof(PermittedStructureLink))]
    [ProtoInclude(103, typeof(DataObjectWithKeyOfLong))]
    public abstract class DataObject
    {
        private DBACTION _DBAction = DBACTION.NONE;

        [DataMember]
        [ProtoMember(1)]
        public DBACTION DBAction 
        {
            get => _DBAction;
            set => _DBAction = value;
        }

        /// <summary>
        /// Shadow property for Protobuf serialization - sends DBAction as integer
        /// </summary>
        [ProtoMember(2)]
        public int DBActionAsInt
        {
            get => (int)_DBAction;
            set => _DBAction = (DBACTION)value;
        }
    }

    /// <summary>
    /// A generic database object that exposes a key value
    /// </summary>
    [DataContract]
    [ProtoContract]
    [ProtoInclude(200, typeof(DataObjectWithParentOfLong))]
    [ProtoInclude(201, typeof(Location))]
    [ProtoInclude(202, typeof(LocationPositionOnly))]
    public class DataObjectWithKeyOfLong : DataObject
    {
        private Int64 _ID;

        [ProtoMember(3, DataFormat = DataFormat.FixedSize)]
        [DataMember]
        public Int64 ID
        {
            get => _ID;
            set => _ID = value;
        }
    }
    
    /// <summary>
    /// A generic database object that exposes an ID value and Parent of
    /// the same type referring to a row in the same table
    /// </summary>
    [DataContract]
    [ProtoContract]
    [ProtoInclude(300, typeof(Structure))]
    [ProtoInclude(301, typeof(StructureType))]
    public class DataObjectWithParentOfLong : DataObjectWithKeyOfLong
    {
        private Int64? _ParentID;

        [ProtoMember(4)]
        [DataMember]
        public Int64? ParentID
        {
            get
            {
                if (_ParentID.HasValue)
                    return _ParentID;
                else
                {
                    return null;
                }
            }
            set => _ParentID = value;
        }
    }
}
