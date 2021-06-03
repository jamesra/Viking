using ProtoBuf;

namespace Viking.gRPC.AnnotationTypes
{

    [ProtoContract]
    /* Recoded [DataContract] */
    public class CreateStructureRetval
    {
        private Structure _structure;
        private Location _location;

        [ProtoMember(1)]
        /* Recoded [DataMember] */
        public Structure structure { get { return _structure; } set { _structure = value; } }

        [ProtoMember(2)]
        /* Recoded [DataMember] */
        public Location location { get { return _location; } set { _location = value; } }

        public CreateStructureRetval(Structure s, Location l)
        {
            _structure = s;
            _location = l;
        }

        public CreateStructureRetval()
        {
        }
    }
}




