using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Dialog text and a trace line for a failed save.
    /// Entity Framework update exceptions and other wrappers keep the reason on an inner exception.
    /// <see cref="Exception.Message"/> alone is the generic wrapper ("See the inner exception for details")
    /// that the link dialog used to show.
    /// Called from the location and structure link commands.
    /// </summary>
    internal static class ExceptionReport
    {
        /// <summary>
        /// Writes <paramref name="exception"/> to the trace log and shows <paramref name="summary"/>
        /// plus each distinct message down the inner-exception chain.
        /// </summary>
        public static void Show(string summary, Exception exception)
        {
            Trace.WriteLine(summary + Environment.NewLine + exception);
            Viking.UI.Forms.ErrorDialog.Show(null, "Recoverable Error",
                summary + Environment.NewLine + Environment.NewLine + Describe(exception));
        }

        /// <summary>
        /// Distinct messages from <paramref name="exception"/> and its <see cref="Exception.InnerException"/>
        /// chain. Callers that only display <see cref="Exception.Message"/> drop the database error.
        /// </summary>
        public static string Describe(Exception exception)
        {
            List<string> messages = [];
            AppendException(exception, messages, 0);
            return string.Join(Environment.NewLine, messages);
        }

        private static void AppendException(Exception? exception, List<string> messages, int depth)
        {
            if (exception is null || depth > 8)
                return;

            Add(messages, exception.Message);

            if (exception is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                    AppendException(inner, messages, depth + 1);
                return;
            }

            AppendException(exception.InnerException, messages, depth + 1);
        }

        private static void Add(List<string> messages, string? message)
        {
            if (string.IsNullOrWhiteSpace(message) || messages.Contains(message))
                return;

            messages.Add(message);
        }
    }
}
