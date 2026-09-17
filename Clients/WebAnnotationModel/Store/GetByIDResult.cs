using System;
using System.Collections.Generic;

namespace WebAnnotationModel
{
    /// <summary>
    /// Result of a bulk fetch or refresh: objects present after the call, and requested keys the server does not have
    /// (never existed or deleted by another client).
    /// Returned by <see cref="IStoreWithKey{KEY, OBJECT}.GetObjectsByIDs"/> and collection
    /// <see cref="IStoreWithKey{KEY, OBJECT}.Refresh(ICollection{KEY}, System.Threading.CancellationToken)"/>.
    /// Not used by cache-only <see cref="IStoreWithKey{KEY, OBJECT}.TryGetObjectsByIDs"/>.
    /// </summary>
    public readonly struct GetByIDResult<KEY, OBJECT>
        where KEY : struct
    {
        /// <summary>
        /// Objects in the local store after the call, in request order.
        /// </summary>
        public IReadOnlyList<OBJECT> Found { get; }

        /// <summary>
        /// Requested keys that are not on the server after this call (never existed or deleted), in request order.
        /// This is not a cache miss; cache-only misses come from <see cref="IStoreWithKey{KEY, OBJECT}.TryGetObjectsByIDs"/>.
        /// </summary>
        public IReadOnlyList<KEY> Missing { get; }

        /// <summary>
        /// True when every requested key was found (<see cref="Missing"/> is empty).
        /// </summary>
        public bool AllFound => Missing.Count == 0;

        /// <summary>
        /// Empty found and missing lists. Used when the caller requested no keys.
        /// </summary>
        public static GetByIDResult<KEY, OBJECT> Empty { get; } =
            new GetByIDResult<KEY, OBJECT>(Array.Empty<OBJECT>(), Array.Empty<KEY>());

        /// <summary>
        /// Null lists are stored as empty.
        /// </summary>
        public GetByIDResult(IReadOnlyList<OBJECT> found, IReadOnlyList<KEY> missing)
        {
            Found = found ?? Array.Empty<OBJECT>();
            Missing = missing ?? Array.Empty<KEY>();
        }
    }
}
