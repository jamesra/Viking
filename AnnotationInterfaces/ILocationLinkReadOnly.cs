using System;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface ILocationLinkReadOnly : IEquatable<ILocationLinkReadOnly>, IComparable<ILocationLinkReadOnly>
    {
        ulong A { get; }
        ulong B { get; }
    }

    public interface ILocationLink : IEquatable<ILocationLink>, IDataObjectWithKey<ILocationLinkReadOnly>
    {
        ulong A { get; set; }
        ulong B { get; set; }
    }
}
