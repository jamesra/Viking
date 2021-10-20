using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class PermittedStructureLink : IPermittedStructureLink
    {
        public ulong SourceTypeID { get => SourceTypeID; set => SourceTypeID = value; }
        public ulong TargetTypeID { get => TargetTypeID; set => TargetTypeID = value; }
        public bool Directional { get => Directional; set => Directional = value; }

        public bool Equals(IPermittedStructureLink other)
        {
            if (ReferenceEquals(other, this))
                return true;

            if (ReferenceEquals(other, null))
                return false;

            return SourceTypeID == other.SourceTypeID &&
                   TargetTypeID == other.TargetTypeID;
        }
    }
}
