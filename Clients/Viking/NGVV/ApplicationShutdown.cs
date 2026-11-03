using System;
using System.Diagnostics;
using System.Threading;

namespace Viking
{
    /// <summary>
    /// Process-wide signal that Viking is closing. Cancelled once, when the main window is destroyed.
    /// </summary>
    /// <remarks>
    /// Work that should stop at exit takes <see cref="Token"/>, alone or linked with its own token. Every gRPC call
    /// gets it through <see cref="Services.Grpc.ShutdownCancellationInterceptor"/>, so in-flight segmentation and
    /// correction calls end before teardown waits for the gRPC channels to shut down.
    /// </remarks>
    public static class ApplicationShutdown
    {
        private static readonly CancellationTokenSource Source = new();

        /// <summary>Cancelled when <see cref="Begin"/> runs. Never disposed, so it is safe to use at any time.</summary>
        public static CancellationToken Token => Source.Token;

        /// <summary>True once <see cref="Begin"/> has run.</summary>
        public static bool HasBegun => Source.IsCancellationRequested;

        /// <summary>
        /// Cancels <see cref="Token"/>. Called by <see cref="VikingApplicationContext"/> on the UI thread after the exit
        /// watchdog is armed. Registered callbacks run synchronously here; their exceptions are logged, not thrown.
        /// Later calls do nothing.
        /// </summary>
        public static void Begin()
        {
            try
            {
                Source.Cancel();
            }
            catch (AggregateException ex)
            {
                Trace.WriteLine($"Error in shutdown cancellation callback: {ex.GetBaseException().Message}");
            }
        }
    }
}
