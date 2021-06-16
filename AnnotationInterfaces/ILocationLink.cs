using System;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface ILocationLink : IEquatable<ILocationLink>
    {
        ulong A { get; }
        ulong B { get; }
    }
}
