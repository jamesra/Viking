using System;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Raises one callback on the UI thread after the view has stopped moving for a fixed interval.
    /// </summary>
    /// <remarks>
    /// <see cref="System.Timers.Timer"/> raises <c>Elapsed</c> on a thread-pool thread, so the settle callback is
    /// posted through <c>postToUi</c> rather than run there: it reads and writes UI-thread state (point lists,
    /// views, camera). <see cref="Restart"/> and <see cref="Dispose"/> are called from the UI thread. After
    /// <see cref="Dispose"/> a callback that was already posted is discarded, because disposing cannot recall it.
    /// </remarks>
    internal sealed class PanZoomDebouncer : IDisposable
    {
        private readonly System.Timers.Timer timer;
        private readonly Action onSettled;
        private readonly Action<Action> postToUi;
        private volatile bool disposed;

        /// <param name="intervalMs">Quiet time after the last <see cref="Restart"/> before the callback fires.</param>
        /// <param name="onSettled">Runs on the UI thread, at most once per settle.</param>
        /// <param name="postToUi">Queues an action on the UI thread. May be a no-op when there is no dispatcher.</param>
        public PanZoomDebouncer(int intervalMs, Action onSettled, Action<Action> postToUi)
        {
            this.onSettled = onSettled ?? throw new ArgumentNullException(nameof(onSettled));
            this.postToUi = postToUi ?? throw new ArgumentNullException(nameof(postToUi));
            timer = new System.Timers.Timer(intervalMs) { AutoReset = false };
            timer.Elapsed += OnElapsed;
        }

        /// <summary>Starts or restarts the quiet interval. Ignored after <see cref="Dispose"/>.</summary>
        public void Restart()
        {
            if (disposed)
                return;

            timer.Stop();
            timer.Start();
        }

        private void OnElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (disposed)
                return;

            postToUi(() =>
            {
                if (!disposed)
                    onSettled();
            });
        }

        /// <summary>Stops the timer and discards any settle that is posted but has not run yet.</summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            timer.Stop();
            timer.Elapsed -= OnElapsed;
            timer.Dispose();
        }
    }
}
