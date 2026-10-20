using System;
using System.Drawing;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    /// <summary>
    /// Modal error dialog whose text sits in a read-only, selectable text box with a Copy button,
    /// so users can paste the message into an error report. Replaces
    /// <see cref="ThreadExceptionDialog"/>, whose message cannot be selected.
    /// Must be shown on the UI thread; <see cref="Show(IWin32Window?, string, string)"/> blocks until closed.
    /// </summary>
    public sealed class ErrorDialog : Form
    {
        private readonly TextBox messageBox;

        private ErrorDialog(string title, string message)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            ClientSize = new Size(640, 360);
            MinimumSize = new Size(360, 200);

            messageBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                Text = message.Replace("\r\n", "\n").Replace("\n", Environment.NewLine),
                BackColor = SystemColors.Window,
            };

            Button copy = new() { Text = "&Copy", AutoSize = true, DialogResult = DialogResult.None };
            copy.Click += (_, _) => CopyToClipboard();

            Button ok = new() { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };

            FlowLayoutPanel buttons = new()
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(6),
            };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(copy);

            Controls.Add(messageBox);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = ok;

            Shown += (_, _) => messageBox.Select(0, 0);
        }

        /// <summary>
        /// Shows the dialog modally.
        /// </summary>
        /// <param name="owner">Parent window, or null for none.</param>
        /// <param name="title">Caption.</param>
        /// <param name="message">Full text, including any stack trace; copied verbatim by the Copy button.</param>
        public static void Show(IWin32Window? owner, string title, string message)
        {
            using ErrorDialog dlg = new(title, message);
            dlg.ShowDialog(owner);
        }

        /// <summary>Shows an exception's full <see cref="Exception.ToString"/> output.</summary>
        public static void Show(IWin32Window? owner, string title, Exception exception)
            => Show(owner, title, exception.ToString());

        private void CopyToClipboard()
        {
            try
            {
                Clipboard.SetText(messageBox.Text);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Clipboard is locked by another process; the text can still be selected manually.
            }
        }
    }
}
