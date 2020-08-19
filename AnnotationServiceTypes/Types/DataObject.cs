using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using ProtoBuf;


namespace AnnotationService.Types
{
    // I can't use straight inheritance because the relationships do not marshal.  So use interfaces instead

    /// <summary>
    /// A generic database object
    /// </summary>
    [DataContract]
    [ProtoContract]
    [ProtoInclude(1, typeof(LocationLink))]
    [ProtoInclude(2, typeof(StructureLink))]
    [ProtoInclude(3, typeof(PermittedStructureLink))]
    public abstract class DataObject
    {
        private DBACTION _DBAction = DBACTION.NONE;

        [DataMember]
        [ProtoMember(10)]
        public DBACTION DBAction
        {
            get { return _DBAction; }
            set { _DBAction = value; }
        }
    }

    /// <summary>
    /// A generic database object that exposes a key value
    /// </summary>
    [DataContract]
    [ProtoContract]
    [ProtoInclude(1, typeof(DataObjectWithParentOfLong))]
    [ProtoInclude(2, typeof(Location))]
    [ProtoInclude(3, typeof(LocationPositionOnly))]
    public class DataObjectWithKeyOfLong : DataObject
    {
        private Int64 _ID;

        [ProtoMember(10)]
        [DataMember]
        public Int64 ID
        {
            get { return _ID; }
            set { _ID = value; }
        }
    }

    /// <summary>
    /// A generic database object that exposes an ID value and Parent of
    /// the same type referring to a row in the same table
    /// </summary>
    [DataContract]
    [ProtoContract]
    [ProtoInclude(1, typeof(Structure))]
    [ProtoInclude(2, typeof(StructureType))]
    public class DataObjectWithParentOfLong : DataObjectWithKeyOfLong
    {
        private Int64? _ParentID;

        [ProtoMember(10)]
        [DataMember]
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
