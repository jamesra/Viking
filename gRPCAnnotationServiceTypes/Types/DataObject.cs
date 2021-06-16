using ProtoBuf;


namespace Viking.AnnotationServiceTypes.gRPC
{
    // I can't use straight inheritance because the relationships do not marshal.  So use interfaces instead

    /// <summary>
    /// A generic database object
    /// </summary>
    /* Recoded [DataContract] */
    [ProtoContract]
    [ProtoInclude(1, typeof(LocationLink))]
    [ProtoInclude(2, typeof(StructureLink))]
    [ProtoInclude(3, typeof(PermittedStructureLink))]
    public abstract class DataObject
    {
        private DBACTION _DBAction = DBACTION.NONE;

        /* Recoded [DataMember] */
        [ProtoMember(10)]
        public DBACTION DBAction
        {
            get { return _DBAction; }
            set { _DBAction = value; }
        }
    }
}

