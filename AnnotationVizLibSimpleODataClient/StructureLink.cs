using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections.Generic;

namespace AnnotationVizLib.SimpleOData
{
    class StructureLink : IStructureLinkKey
    {
        public static StructureLink FromDictionary(IDictionary<string, object> dict)
        {
            StructureLink s = new StructureLink
            {
                SourceID = System.Convert.ToUInt64(dict["SourceID"]),
                TargetID = System.Convert.ToUInt64(dict["TargetID"]),
                Bidirectional = System.Convert.ToBoolean(dict["Bidirectional"])
            };

            return s;
        }

        public StructureLink()
        {
        }

        public bool Directional
        {
            get
            {
                return !Bidirectional;
            }
            set { Bidirectional = !value; }
        }

        private bool Bidirectional { get; set; }

        public ulong SourceID
        {
            get; private set;
        }

        public ulong TargetID
        {
            get; private set;
        }

        public override string ToString()
        {
            if (Bidirectional)
                return string.Format("{0} <-> {1}", SourceID, TargetID);
            else
                return string.Format("{0}  -> {1}", SourceID, TargetID);
        }

        public bool Equals(IStructureLinkKey other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.SourceID == this.SourceID &&
                other.TargetID == this.TargetID &&
                other.Directional == this.Directional)
                return true;

            return false;
        }

        public bool Equals(StructureLink other)
        {
            return this.Equals((IStructureLinkKey)other);
        }

        public int CompareTo(IStructureLinkKey other)
        {
            if (other is null)
                return -1;

            if (Bidirectional.Equals(!other.Directional) && Bidirectional)
            {
                var A_Low = Math.Min(SourceID, TargetID);
                var A_High = Math.Max(SourceID, TargetID);

                var B_Low = Math.Min(other.SourceID, other.TargetID);
                var B_High = Math.Max(other.SourceID, other.TargetID);

                int lowCompare = A_Low.CompareTo(B_Low);
                if (lowCompare != 0)
                    return lowCompare;

                int highCompare = B_High.CompareTo(B_High);
                if (highCompare != 0)
                    return highCompare;
            }
            else
            {
                int sourceCompare = this.SourceID.CompareTo(other.SourceID);
                if (sourceCompare != 0)
                    return sourceCompare;

                int targetCompare = this.TargetID.CompareTo(other.TargetID);
                if (targetCompare != 0)
                    return targetCompare;
            }

            return this.Bidirectional.CompareTo(!other.Directional);
        }
    }
}
