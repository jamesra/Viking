using System;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class LocationLink : ILocationLink
    {
        ulong ILocationLink.A => (ulong)SourceId;
        ulong ILocationLink.B => (ulong)TargetId;

        public ulong OtherKey(ulong key)
        {
            if ((ulong)SourceId == key)
                return (ulong)TargetId;
            if ((ulong)TargetId == key)
                return (ulong)SourceId;

            throw new ArgumentException($"{key} is not part of location link {SourceId}-{TargetId}");
        }

        ILocationLinkKey IDataObjectWithKey<ILocationLinkKey>.ID
        {
            get => new LocationLinkKey(SourceId, TargetId);
            set => throw new NotSupportedException();
        }

        LocationLinkKey IDataObjectWithKey<LocationLinkKey>.ID
        {
            get => new LocationLinkKey(SourceId, TargetId);
            set => throw new NotSupportedException();
        }

        public bool Equals(ILocationLink other)
        {
            if (ReferenceEquals(other, this))
                return true;

            if (other is null)
                return false;

            return ((ulong)SourceId == other.A && (ulong)TargetId == other.B) ||
                   ((ulong)SourceId == other.B && (ulong)TargetId == other.A);
        }
    }
}
