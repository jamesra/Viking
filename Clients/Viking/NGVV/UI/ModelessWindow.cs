#nullable enable

using System;
using System.Windows;
using System.Windows.Forms.Integration;

namespace Viking.UI
{
    /// <summary>
    /// Shows a WPF window modelessly from Viking's Windows Forms message loop so that it gets the keyboard. The Annotation
    /// and Viewer Preferences dialogs open through <see cref="ShowFromWinForms"/>; the change log passes
    /// <see cref="ElementHost.EnableModelessKeyboardInterop"/> to <c>ChangelogWindow.Show</c> (see
    /// <see cref="ChangelogStartup"/>), and the annotation overlay's small go-to and find forms call it themselves.
    /// </summary>
    /// <remarks>
    /// Viking's UI thread runs the Windows Forms loop (<c>Application.Run</c>), not WPF's dispatcher loop. A WPF window
    /// shown there with plain <see cref="Window.Show"/> receives almost no keyboard input: on 2026-10-29 (Legacy), with keys
    /// posted to such a window, digits, '.', ',' and the slider's arrow keys never arrived and Tab did not move focus; only
    /// a key like Backspace did. That is why the preference dialogs' number boxes could not be typed into. WPF reads typed
    /// characters and navigation keys through <see cref="System.Windows.Interop.ComponentDispatcher"/>, which only a WPF
    /// loop pumps; <see cref="ElementHost.EnableModelessKeyboardInterop"/> makes the Forms loop forward them.
    /// <see cref="Window.ShowDialog"/> runs a WPF loop of its own and does not need this, and neither does Jotunn, a WPF
    /// application. This lives in VikingCore, not Viking.UI.WPF as in Legacy, because dev's Viking.UI.WPF does not
    /// reference Windows Forms (Jotunn uses it).
    /// </remarks>
    public static class ModelessWindow
    {
        /// <summary>Routes Windows Forms keyboard messages to <paramref name="window"/>, then shows it modelessly.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="window"/> is null.</exception>
        public static void ShowFromWinForms(Window window)
        {
            if (window is null)
                throw new ArgumentNullException(nameof(window));

            ElementHost.EnableModelessKeyboardInterop(window);
            window.Show();
        }
    }
}
