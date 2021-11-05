using System;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface ILocationLinkKey : IEquatable<ILocationLinkKey>, IComparable<ILocationLinkKey>
    {
        ulong A { get; }
        ulong B { get; }

        /// <summary>
        /// Returns the side of the link that doesn't match the passed key.
        /// Throws an exception if the passed key does not match either A or B
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        ulong OtherKey(ulong key);
    }

    public interface ILocationLink : IEquatable<ILocationLink>, IDataObjectWithKey<ILocationLinkKey>, IDataObjectWithKey<LocationLinkKey>
    {
        ulong A { get; }
        ulong B { get; }

        /// <summary>
        /// Returns the side of the link that doesn't match the passed key.
        /// Throws an exception if the passed key does not match either A or B
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        ulong OtherKey(ulong key);
    }
}
