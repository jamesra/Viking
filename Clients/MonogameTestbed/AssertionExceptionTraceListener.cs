using System;
using System.Diagnostics;

namespace MonogameTestbed
{
    /// <summary>
    /// Raised in place of process termination when a <see cref="Debug.Assert(bool)"/> fails.
    /// </summary>
    public class AssertionFailedException : Exception
    {
        public AssertionFailedException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Turns a failed <see cref="Debug.Assert(bool)"/> into an <see cref="AssertionFailedException"/> on the
    /// asserting thread instead of letting <see cref="DefaultTraceListener"/> call Environment.FailFast.
    ///
    /// A whole-cell run meshes thousands of slices on worker threads, each already wrapped in a handler that
    /// records the slice as failed and moves on.  With the default listener a single assert in any of them kills
    /// the process, so a debug build could not finish a cell.  Throwing routes the assert into those same
    /// handlers: the slice is logged with the assert message and stack, written to the failed-slice repro file,
    /// and the rest of the run proceeds.  When a debugger is attached it still breaks first, so the assert can be
    /// inspected at the point of failure before the exception unwinds.
    ///
    /// Must be the first listener in <see cref="Trace.Listeners"/>: TraceInternal.Fail calls each listener in
    /// order, and the throw here is what prevents the DefaultTraceListener's FailFast from running.
    /// </summary>
    public sealed class AssertionExceptionTraceListener : TraceListener
    {
        public AssertionExceptionTraceListener() : base("AssertionToException")
        {
        }

        public static void Install()
        {
            foreach (TraceListener listener in Trace.Listeners)
            {
                if (listener is AssertionExceptionTraceListener)
                    return;
            }

            Trace.Listeners.Insert(0, new AssertionExceptionTraceListener());
        }

        /// <summary>Output is left to the console and log listeners.</summary>
        public override void Write(string message)
        {
        }

        public override void WriteLine(string message)
        {
        }

        public override void Fail(string message) => Fail(message, null);

        public override void Fail(string message, string detailMessage)
        {
            string text = string.IsNullOrEmpty(detailMessage)
                ? $"Assertion failed: {message}"
                : $"Assertion failed: {message}\n{detailMessage}";

            //Trace's global lock is reentrant on this thread, so logging from inside Fail is safe.  The catch
            //sites also log the exception, but this line makes an assert stand out from an ordinary failure.
            Trace.WriteLine($"{text}\n{new StackTrace(2, true)}");

            if (Debugger.IsAttached)
                Debugger.Break();

            throw new AssertionFailedException(text);
        }
    }
}
