using System;

namespace Annotation.Interfaces
{
    public interface ILocationLink : IEquatable<ILocationLink>
    {
        ulong A { get; }
        ulong B { get; }
    }
}
