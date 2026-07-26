using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class PermittedStructureLink : IPermittedStructureLinkReadOnly
    {
        ulong IPermittedStructureLinkReadOnly.SourceTypeID => (ulong)this.SourceTypeId;
        ulong IPermittedStructureLinkReadOnly.TargetTypeID => (ulong)this.TargetTypeId;
        bool IPermittedStructureLinkReadOnly.Directional => !this.Bidirectional;

        public bool Equals(IPermittedStructureLinkReadOnly other)
        {
            if (ReferenceEquals(other, this))
                return true;

            if (ReferenceEquals(other, null))
                return false;

            return ((IPermittedStructureLinkReadOnly)this).SourceTypeID == other.SourceTypeID &&
                   ((IPermittedStructureLinkReadOnly)this).TargetTypeID == other.TargetTypeID;
        }
    }
}
