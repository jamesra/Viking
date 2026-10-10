using System;
using System.Threading.Tasks;

namespace Viking.Common
{
    /// <summary>
    /// Task composition helpers for pipelines whose second stage is itself asynchronous.
    /// </summary>
    public static class TaskChaining
    {
        /// <summary>
        /// Awaits <paramref name="first"/>, then awaits the task returned by <paramref name="next"/> for its result.
        /// The returned task completes only after the second stage finishes and carries its exception, unlike
        /// <c>first.ContinueWith(t => next(t.Result))</c>, which yields a <c>Task&lt;Task&gt;</c> that completes as soon as
        /// the second stage starts. Used by the tile export loops so their throttles count a tile until its PNG is written.
        /// </summary>
        public static async Task ThenAsync<T>(this Task<T> first, Func<T, Task> next)
        {
            T result = await first.ConfigureAwait(false);
            await next(result).ConfigureAwait(false);
        }
    }
}
