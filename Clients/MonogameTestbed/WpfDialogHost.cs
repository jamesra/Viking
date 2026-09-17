using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace MonogameTestbed
{
    /// <summary>
    /// Shows WPF dialogs from the MonoGame thread via a dedicated STA dispatcher.
    /// MonoGame blocks the main thread in Game.Run, so WPF cannot share that thread.
    /// A single long-lived STA thread is used because only one <see cref="Application"/> is allowed
    /// per AppDomain — creating a new Application per Help open fails on the second open.
    /// </summary>
    static class WpfDialogHost
    {
        private static int _showing;
        private static Thread _staThread;
        private static Dispatcher _dispatcher;
        private static readonly ManualResetEventSlim _dispatcherReady = new(false);

        /// <summary>
        /// Opens the hotkey Help window and blocks until it closes. Nested calls are ignored.
        /// </summary>
        public static void ShowHotkeyHelp(string testTitle, IReadOnlyList<HotkeyHelpSection> sections)
        {
            if (Interlocked.CompareExchange(ref _showing, 1, 0) != 0)
                return;

            try
            {
                EnsureStaDispatcher();

                Exception error = null;
                _dispatcher.Invoke(() =>
                {
                    try
                    {
                        //ShowDialog pumps this dispatcher until Close; no per-call Application required.
                        var window = new HotkeyHelpWindow(testTitle, sections)
                        {
                            WindowStartupLocation = WindowStartupLocation.CenterScreen
                        };
                        window.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                });

                if (error != null)
                    throw new InvalidOperationException($"Help dialog failed: {error.Message}", error);
            }
            finally
            {
                Interlocked.Exchange(ref _showing, 0);
            }
        }

        static void EnsureStaDispatcher()
        {
            if (_dispatcher != null)
                return;

            var thread = new Thread(() =>
            {
                //CurrentDispatcher creates the dispatcher for this STA thread; Run keeps it alive for reuse.
                _dispatcher = Dispatcher.CurrentDispatcher;
                _dispatcherReady.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "MonogameTestbed.WpfHelp"
            };

            thread.SetApartmentState(ApartmentState.STA);

            Thread previous = Interlocked.CompareExchange(ref _staThread, thread, null);
            if (previous != null)
            {
                _dispatcherReady.Wait();
                return;
            }

            thread.Start();
            _dispatcherReady.Wait();
        }
    }
}
