using ProtoBuf;
using System;

namespace Viking.gRPC.AnnotationTypes
{
    /* Recoded [DataContract] */
    [Serializable]
    [ProtoContract]
    public class AnnotationSet
    {
        /* Recoded [DataMember] */
        [ProtoMember(1)]
        public Structure[] Structures { get; private set; }

        /* Recoded [DataMember] */
        [ProtoMember(2)]
        public Location[] Locations { get; private set; }

        public AnnotationSet(Structure[] structs, Location[] locs)
        {
            this.Structures = structs;
            this.Locations = locs;
        }

        public AnnotationSet()
        {

        }
    }
}

