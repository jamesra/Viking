using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.Windows.Forms;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Dialog text and a trace line for a failed save.
    /// WCF <see cref="FaultException{ExceptionDetail}"/> and Entity Framework update exceptions
    /// keep the SQL reason on an inner exception. <see cref="Exception.Message"/> alone is the
    /// generic wrapper ("See the inner exception for details") that the link dialog used to show.
    /// Called from the location and structure link commands.
    /// </summary>
    internal static class ExceptionReport
    {
        /// <summary>
        /// Writes <paramref name="exception"/> to the trace log and shows <paramref name="summary"/>
        /// plus each distinct message down the inner-exception and fault-detail chain.
        /// </summary>
        public static void Show(string summary, Exception exception)
        {
            Trace.WriteLine(summary + Environment.NewLine + exception);
            MessageBox.Show(summary + Environment.NewLine + Environment.NewLine + Describe(exception), "Recoverable Error");
        }

        /// <summary>
        /// Distinct messages from <paramref name="exception"/>, its <see cref="Exception.InnerException"/>
        /// chain, and a WCF <see cref="ExceptionDetail"/> chain. Callers that only display
        /// <see cref="Exception.Message"/> drop the database error.
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

            if (exception is FaultException<ExceptionDetail> fault)
                AppendDetail(fault.Detail, messages, depth + 1);

            if (exception is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                    AppendException(inner, messages, depth + 1);
                return;
            }

            AppendException(exception.InnerException, messages, depth + 1);
        }

        private static void AppendDetail(ExceptionDetail? detail, List<string> messages, int depth)
        {
            if (detail is null || depth > 8)
                return;

            Add(messages, detail.Message);
            AppendDetail(detail.InnerException, messages, depth + 1);
        }

        private static void Add(List<string> messages, string? message)
        {
            if (string.IsNullOrWhiteSpace(message) || messages.Contains(message))
                return;

            messages.Add(message);
        }
    }
}
