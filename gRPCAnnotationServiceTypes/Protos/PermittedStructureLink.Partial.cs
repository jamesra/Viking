using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public static class PermittedStructureLinkExtensions
    {
        public static PermittedStructureLink ToPermittedStructureLink(this IPermittedStructureLink src)
        {
            var output = new PermittedStructureLink()
            {
                SourceTypeId = (long)src.SourceTypeID,
                TargetTypeId = (long)src.TargetTypeID,
                Bidirectional = !src.Directional  
            };

            return output;
        }
    }

    public partial class PermittedStructureLink : IPermittedStructureLink
    {
        public IPermittedStructureLinkKey ID { get => new PermittedStructureLinkKey(SourceTypeId, TargetTypeId, bidirectional_); set => throw new NotImplementedException(); }

        ulong IPermittedStructureLink.SourceTypeID { get => (ulong)SourceTypeId; set => SourceTypeId = (long)value; }

        ulong IPermittedStructureLink.TargetTypeID { get => (ulong)TargetTypeId; set => TargetTypeId = (long)value; }

        bool IPermittedStructureLink.Directional { get => Bidirectional; set => Bidirectional = !value; }

        PermittedStructureLinkKey IDataObjectWithKey<PermittedStructureLinkKey>.ID { get => new PermittedStructureLinkKey(SourceTypeId, TargetTypeId, bidirectional_); set => throw new NotImplementedException(); }

        public bool Equals(IPermittedStructureLink other)
        {
            if (ReferenceEquals(other, this))
                return true;

            if (other is null)
                return false;

            return (ulong)SourceTypeId == other.SourceTypeID &&
                   (ulong)TargetTypeId == other.TargetTypeID &&
                   Bidirectional != other.Directional;
        }

        bool IEquatable<IPermittedStructureLink>.Equals(IPermittedStructureLink other)
        {
            if (ReferenceEquals(other, this))
                return true;

            if (other is null)
                return false;

            return (ulong)SourceTypeId == other.SourceTypeID &&
                   (ulong)TargetTypeId == other.TargetTypeID &&
                   Bidirectional != other.Directional;
        } 
    }
}
