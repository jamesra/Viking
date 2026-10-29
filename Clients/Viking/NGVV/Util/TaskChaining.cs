using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Reads a value with <paramref name="get"/>, runs <paramref name="body"/>, and once it has finished, failed or been canceled
        /// writes the value it read back with <paramref name="set"/>; the body's exception, if any, still reaches the caller. The
        /// value is read once, before the body starts, so the body may change it any number of times. Used by the frame and tile
        /// exports, whose renders switch the viewer's section per tile, to put the viewer back on the section the user was viewing.
        /// The await keeps the caller's context, so a UI caller's <paramref name="set"/> runs on the UI thread.
        /// </summary>
        public static async Task RestoreAfterAsync<T>(Func<T> get, Action<T> set, Func<Task> body)
        {
            T original = get();
            try
            {
                await body();
            }
            finally
            {
                set(original);
            }
        }

        /// <summary>
        /// Awaits finished tasks, removing them from <paramref name="pending"/>, until at most <paramref name="maxPending"/> remain.
        /// Used to throttle export loops. A task that faulted or was canceled rethrows here instead of vanishing from the list
        /// (the old <c>Task.WhenAny</c> + <c>Remove</c> loops dropped the failure, so a PNG that could not be written went unreported).
        /// On failure the remaining tasks are awaited first so none is still writing when the caller reports the error; their own
        /// failures are not reported (the first failure wins).
        /// </summary>
        public static async Task DrainToAsync(List<Task> pending, int maxPending)
        {
            while (pending.Count > maxPending)
            {
                Task completed = await Task.WhenAny(pending).ConfigureAwait(false);
                pending.Remove(completed);

                if (completed.IsFaulted || completed.IsCanceled)
                {
                    try
                    {
                        await Task.WhenAll(pending).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Only the first failure is reported.
                    }
                    finally
                    {
                        pending.Clear();
                    }

                    await completed.ConfigureAwait(false);
                }
            }
        }
    }
}
