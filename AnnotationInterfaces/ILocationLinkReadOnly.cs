using System;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface ILocationLinkReadOnly : IEquatable<ILocationLinkReadOnly>, IComparable<ILocationLinkReadOnly>
    {
        ulong A { get; }
        ulong B { get; }
    }

    public interface ILocationLink : IEquatable<ILocationLink>{
        ulong A { get; }
        ulong B { get; }
    }
}
