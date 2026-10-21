using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Viking
{
    /// <summary>
    /// Writes a memory dump of Viking when exit teardown stalls, so the stuck threads can be read afterwards
    /// (WinDbg with SOS, or Visual Studio).
    /// </summary>
    /// <remarks>
    /// The exit watchdog calls <see cref="TryCapture"/> before it terminates the process. The dump is written by a
    /// second copy of Viking.exe started with <see cref="HelperArgument"/>: MiniDumpWriteDump on the current process can
    /// deadlock when a suspended thread holds the heap or loader lock, which is likely in a stalled process. Only the
    /// newest dump is kept, in the session log folder. The dump holds process memory, including the sign-in token, and
    /// never leaves the machine.
    /// </remarks>
    public static class ExitStallDump
    {
        /// <summary>First command-line argument that makes Viking.exe write a dump and exit; see <see cref="TryRunHelper"/>.</summary>
        public const string HelperArgument = "--write-exit-stall-dump";

        private const string FilePrefix = "exit-stall-";

        private static readonly TimeSpan HelperTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Same folder <c>Program.CreateDebugListener</c> writes session logs to.
        /// </summary>
        private static string LogDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Viking", "Logs");

        /// <summary>
        /// Dumps the current process through the helper and waits up to 60 s for it. Called by the exit watchdog thread.
        /// Skipped under a debugger. Never throws.
        /// </summary>
        /// <returns>Path of the dump, or null when none was written.</returns>
        public static string? TryCapture()
        {
            if (Debugger.IsAttached)
                return null;

            try
            {
                Directory.CreateDirectory(LogDirectory);
                foreach (string old in Directory.GetFiles(LogDirectory, FilePrefix + "*.dmp"))
                {
                    try
                    {
                        File.Delete(old);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                using Process self = Process.GetCurrentProcess();
                string path = Path.Combine(
                    LogDirectory,
                    $"{FilePrefix}{DateTime.Now:yyyy.MM.dd HH.mm.ss}-{self.Id}.dmp");

                var startInfo = new ProcessStartInfo(self.MainModule!.FileName,
                    $"{HelperArgument} {self.Id.ToString(CultureInfo.InvariantCulture)} \"{path}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process? helper = Process.Start(startInfo);
                if (helper is null)
                    return null;

                if (!helper.WaitForExit((int)HelperTimeout.TotalMilliseconds))
                {
                    try
                    {
                        helper.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    return null;
                }

                return File.Exists(path) ? path : null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exit stall dump failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Runs the helper side when <paramref name="args"/> is <c>--write-exit-stall-dump &lt;pid&gt; &lt;path&gt;</c>.
        /// Called first thing in <c>Program.Main</c>, before Velopack and any UI; Main returns when this returns true.
        /// </summary>
        /// <returns>True when the arguments selected the helper, whether or not the dump succeeded.</returns>
        public static bool TryRunHelper(string[] args)
        {
            if (args is null || args.Length != 3 || !string.Equals(args[0], HelperArgument, StringComparison.Ordinal))
                return false;

            if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid))
                return true;

            string path = args[2];
            bool written = false;
            try
            {
                using Process target = Process.GetProcessById(pid);
                using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    written = MiniDumpWriteDump(target.Handle, (uint)pid, file.SafeFileHandle, DumpType,
                        IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch (Exception)
            {
                // The target may have exited, or the watchdog gave up and terminated it mid-dump.
            }

            if (!written)
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception)
                {
                }
            }

            return true;
        }

        /// <summary>
        /// The set <c>procdump -mp</c> writes: thread, handle and module data plus private read/write memory, which holds
        /// the managed heap SOS needs for managed stacks, without image pages.
        /// </summary>
        private const uint DumpType =
            0x00000001 | // MiniDumpWithDataSegs
            0x00000004 | // MiniDumpWithHandleData
            0x00000020 | // MiniDumpWithUnloadedModules
            0x00000200 | // MiniDumpWithPrivateReadWriteMemory
            0x00000800 | // MiniDumpWithFullMemoryInfo
            0x00001000;  // MiniDumpWithThreadInfo

        [DllImport("dbghelp.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint processId,
            SafeFileHandle hFile,
            uint dumpType,
            IntPtr exceptionParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);
    }
}
