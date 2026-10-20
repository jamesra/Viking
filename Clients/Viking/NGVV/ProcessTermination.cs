using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Viking
{
    /// <summary>
    /// Ends the Viking process without running .NET Framework managed shutdown.
    /// </summary>
    /// <remarks>
    /// <see cref="Environment.Exit(int)"/> raises <see cref="AppDomain.ProcessExit"/>, and Grpc.Core registers a
    /// handler there that waits with no timeout for every channel and its completion-queue threads to stop. A gRPC
    /// call still in flight when the main window closes (segmentation, section correction) keeps that wait open, so
    /// the process lived on with no window. The exit watchdog's own <see cref="Environment.Exit(int)"/> stalled the
    /// same way. Viking saves nothing during exit (settings are saved when they change), so skipping
    /// <see cref="AppDomain.ProcessExit"/>, finalizers and DLL detach loses nothing.
    /// </remarks>
    public static class ProcessTermination
    {
        private static readonly TimeSpan FlushTimeout = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Flushes trace listeners, then terminates the process. Does not return.
        /// Called at the end of <c>Program.Main</c> after teardown, and by the exit watchdog in
        /// <see cref="VikingApplicationContext"/> when teardown stalls. Safe from any thread and after the log
        /// writer is closed.
        /// </summary>
        /// <param name="exitCode">Process exit code.</param>
        public static void Terminate(int exitCode)
        {
            // Trace.UseGlobalLock is on, so a thread stuck inside a listener would block Flush; bound the wait.
            try
            {
                Task.Run(static () =>
                {
                    try
                    {
                        Trace.Flush();
                    }
                    catch (Exception)
                    {
                        // The log writer may already be closed.
                    }
                }).Wait(FlushTimeout);
            }
            catch (Exception)
            {
            }

            TerminateProcess(GetCurrentProcess(), unchecked((uint)exitCode));

            // TerminateProcess on the current process does not return; this runs only if the call itself failed.
            Environment.Exit(exitCode);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
    }
}
