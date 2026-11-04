using System;

namespace Viking.SegmentationPrompts
{
    /// <summary>The targets cover more tiles or shapes than one request may carry.</summary>
    public sealed class PromptTooLargeException : Exception
    {
        /// <summary>A prompt over a limit; the message names the count and the limit.</summary>
        public PromptTooLargeException(string message) : base(message)
        {
        }
    }
}
