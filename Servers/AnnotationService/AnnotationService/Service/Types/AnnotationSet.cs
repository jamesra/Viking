using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;

namespace Annotation
{
    [DataContract]
    [Serializable]
    public class AnnotationSet
    {
        [DataMember]
        public Structure[] Structures { get; private set; }

        [DataMember]
        public Location[] Locations { get; private set; }

        public AnnotationSet(Structure[] structs, Location[] locs)
        {
            this.Structures = structs;
            this.Locations = locs;
        }
    }
}
