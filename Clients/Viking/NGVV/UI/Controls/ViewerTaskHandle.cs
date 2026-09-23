using System;
using System.Threading;

namespace Viking.UI.Controls
{
    /// <summary>
    /// One job shown on the section viewer status bar.
    /// Dispose when the job ends, including after cancel or failure, to hide the progress text, bar, and Cancel button.
    /// The Cancel button cancels the caller's <see cref="System.Threading.CancellationTokenSource"/>; this handle does not dispose that source.
    /// Only one handle can be open. A second <see cref="SectionViewerControl.TryBeginViewerTask"/> returns null until this is disposed.
    /// Dispose is safe from a background thread.
    /// </summary>
    public sealed class ViewerTaskHandle : IDisposable
    {
        private Action? _onDispose;

        /// <summary>Post updates here from the background job. Reports are marshalled to the viewer thread.</summary>
        public IProgress<ViewerTaskProgress> Progress { get; }

        internal ViewerTaskHandle(IProgress<ViewerTaskProgress> progress, Action onDispose)
        {
            Progress = progress;
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            Action? dispose = Interlocked.Exchange(ref _onDispose, null);
            dispose?.Invoke();
        }
    }
}
