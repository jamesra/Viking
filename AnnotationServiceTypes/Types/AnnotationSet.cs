using ProtoBuf;
using System;
using System.Runtime.Serialization;

namespace AnnotationService.Types
{
    [DataContract]
    [Serializable]
    [ProtoContract]
    public class AnnotationSet
    {
        [DataMember]
        [ProtoMember(1)]
        public Structure[] Structures { get; private set; }

        [DataMember]
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
