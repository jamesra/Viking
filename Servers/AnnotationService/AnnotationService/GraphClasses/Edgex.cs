using System.Runtime.Serialization;

namespace AnnotationService.Types
{
    [DataContract]
    public class Edgex
    {
        [DataMember]
        public long SourceParentID;
        [DataMember]
        public long TargetParentID;
        [DataMember]
        public StructureLink Link;
        [DataMember]
        public string SourceTypeName;

        [DataMember]
        public long SourceID
        {
            get { return Link.SourceID; }
            set { }
        }

        [DataMember]
        public long TargetID
        {
            get { return Link.TargetID; }
            set { }
        }


        public Edgex(long SourceParentID, long TargetParentID, StructureLink link, string SourceTypeName)
        {
            this.SourceParentID = SourceParentID;
            this.TargetParentID = TargetParentID;
            this.Link = link;
            this.SourceTypeName = SourceTypeName;

        }

        /// <summary>
        /// This string lists the parent structures connected, i.e. cells
        /// </summary>

        public string KeyString
        {
            get
            {
                return SourceParentID + "-" + TargetParentID + "," + SourceTypeName;
            }

        }

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

        public override int GetHashCode()
        {
            return System.Convert.ToInt32(SourceID);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Edgex E))
                return false;

            return SourceID == E.SourceID &&
                   TargetID == E.TargetID;
        }
    }
}