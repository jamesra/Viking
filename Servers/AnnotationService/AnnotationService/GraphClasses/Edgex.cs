using System.Runtime.Serialization;

namespace AnnotationService.Types
{
    [DataContract]
    public class Edgex(long SourceParentID, long TargetParentID, StructureLink link, string SourceTypeName)
    {
        [DataMember]
        public long SourceParentID = SourceParentID;
        [DataMember]
        public long TargetParentID = TargetParentID;
        [DataMember]
        public StructureLink Link = link;
        [DataMember]
        public string SourceTypeName = SourceTypeName;

        [DataMember]
        public long SourceID
        {
            get => Link.SourceID;
            set { }
        }

        [DataMember]
        public long TargetID
        {
            get => Link.TargetID;
            set { }
        }

        /// <summary>
        /// This string lists the parent structures connected, i.e. cells
        /// </summary>

        public string KeyString => SourceParentID + "-" + TargetParentID + "," + SourceTypeName;

        /// <summary>
        /// This string lists the actual structures connection, i.e. synapses and gap junction ID's
        /// </summary>

        public string ConnectionString
        {
            get
            {
                string linkstring = "->";
                if (Link.Bidirectional)
                    linkstring = "<->";
                return SourceID + linkstring + TargetID;
            }

        }

        public override int GetHashCode() => System.Convert.ToInt32(SourceID);

        public override bool Equals(object obj)
        {
            if (obj is not Edgex E)
                return false;

            return SourceID == E.SourceID &&
                   TargetID == E.TargetID;
        }
    }
}