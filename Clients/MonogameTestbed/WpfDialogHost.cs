using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;

namespace MonogameTestbed
{
    /// <summary>
    /// Shows WPF dialogs from the MonoGame thread by spinning a temporary STA message loop.
    /// MonoGame blocks the main thread in Game.Run, so WPF cannot share that thread.
    /// </summary>
    static class WpfDialogHost
    {
        private static int _showing;

        /// <summary>
        /// Opens the hotkey Help window and blocks until it closes. Nested calls are ignored.
        /// </summary>
        public static void ShowHotkeyHelp(string testTitle, IReadOnlyList<HotkeyHelpSection> sections)
        {
            if (Interlocked.CompareExchange(ref _showing, 1, 0) != 0)
                return;

            try
            {
                Exception error = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        //A temporary Application owns the dialog's dispatcher for this thread only.
                        var app = new Application
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown
                        };

                        var window = new HotkeyHelpWindow(testTitle, sections);
                        window.Closed += (_, _) => app.Shutdown();
                        app.Run(window);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Name = "MonogameTestbed.WpfHelp";
                thread.Start();
                thread.Join();

                if (error != null)
                    throw new InvalidOperationException("Help dialog failed.", error);
            }
            finally
            {
                Interlocked.Exchange(ref _showing, 0);
            }
        }
    }
}
