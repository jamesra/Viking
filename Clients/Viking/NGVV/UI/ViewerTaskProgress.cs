using System;

namespace Viking.UI.Controls
{
    /// <summary>
    /// Progress snapshot for the section viewer status bar.
    /// Any background job can report through <see cref="ViewerTaskHandle.Progress"/>.
    /// </summary>
    public readonly struct ViewerTaskProgress
    {
        /// <summary>Units finished. The bar uses this with <see cref="Total"/>.</summary>
        public int Completed { get; }

        /// <summary>Units in the job. Zero leaves the bar indeterminate.</summary>
        public int Total { get; }

        /// <summary>Status text, such as the section being processed and how many updates it has made.</summary>
        public string? Message { get; }

        public ViewerTaskProgress(int completed, int total, string? message)
        {
            Completed = completed;
            Total = total;
            Message = message;
        }
    }
}
