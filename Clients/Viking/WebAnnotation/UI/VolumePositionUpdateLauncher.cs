using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.UI;
using Viking.UI.Controls;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Annotation-menu entry for correcting volume positions.
    /// A second launch while a pass is running asks before cancelling it. The summary dialog is part of the running task, so the replacement pass waits until that dialog closes.
    /// </summary>
    internal static class VolumePositionUpdateLauncher
    {
        private static int _menuBusy;
        private static Task? _activeRun;
        private static CancellationTokenSource? _activeCancellation;

        /// <summary>True while a volume position pass is running. Read on the UI thread.</summary>
        private static bool IsRunning => _activeRun is not null && !_activeRun.IsCompleted;

        private static Form? _hookedForm;

        /// <summary>
        /// Subscribes once to the viewer form's closing event so exiting mid-pass asks first.
        /// Called on the UI thread when a pass starts; the handler is a no-op while nothing is running.
        /// </summary>
        private static void HookExitConfirmation(Form? form)
        {
            if (form is null || ReferenceEquals(_hookedForm, form))
                return;

            _hookedForm = form;
            form.FormClosing += (_, e) =>
            {
                if (!e.Cancel && !ConfirmExit(form))
                    e.Cancel = true;
            };
        }

        /// <summary>Returns true when it is fine to close: no pass is running, or the user chose to exit anyway.</summary>
        internal static bool ConfirmExit(IWin32Window? owner)
        {
            if (!IsRunning)
                return true;

            return MessageBox.Show(
                owner,
                "Update Volume Positions is still running. If you exit now it will stop and the remaining sections will not be updated.\n\nAre you sure you want to exit?",
                "Update Volume Positions",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        /// <summary>
        /// Starts the host's progress display for the job (Viking's status-bar task): the progress sink and the handle
        /// that ends the display. Null when the host cannot show it now (another task is showing).
        /// </summary>
        internal delegate (IProgress<ViewerTaskProgress> Progress, IDisposable Finish)? BeginProgressDisplay(
            string title, CancellationTokenSource cancellation);

        /// <summary>Called from the Annotation menu click. Re-checks reviewer access before opening the dialog.</summary>
        public static Task RunAsync()
        {
            // Viking reports the job on the section viewer's status bar; without one, there is nothing to run it from.
            if (State.ViewerControl is null)
            {
                MessageBox.Show("Open a volume before updating volume positions.", "Update Volume Positions",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return Task.CompletedTask;
            }

            return RunAsync(BeginOnViewer);
        }

        /// <summary>Viking's status-bar task on the section viewer.</summary>
        private static (IProgress<ViewerTaskProgress> Progress, IDisposable Finish)? BeginOnViewer(string title, CancellationTokenSource cancellation)
        {
            ViewerTaskHandle? handle = State.ViewerControl?.TryBeginViewerTask(title, cancellation);
            return handle is null ? null : (handle.Progress, handle);
        }

        /// <summary>As <see cref="RunAsync()"/>, for a host that shows the job's progress its own way (Jotunn).</summary>
        internal static async Task RunAsync(BeginProgressDisplay beginProgress)
        {
            if (Interlocked.Exchange(ref _menuBusy, 1) == 1)
                return;

            try
            {
                if (!VolumeAccessRoles.HasReviewAccess())
                    return;

                await RunCoreAsync(beginProgress).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                Viking.UI.Forms.ErrorDialog.Show(State.ViewerForm, "Update Volume Positions", "Volume position update failed.\n\n" + ex);
            }
            finally
            {
                Interlocked.Exchange(ref _menuBusy, 0);
            }
        }

        private static async Task RunCoreAsync(BeginProgressDisplay beginProgress)
        {
            if (_activeRun is not null && !_activeRun.IsCompleted)
            {
                DialogResult replace = MessageBox.Show(
                    "A volume position correction is already running. Cancel it and start a new one?",
                    "Update Volume Positions",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (replace != DialogResult.Yes)
                    return;

                _activeCancellation?.Cancel();
                await _activeRun.ConfigureAwait(true);
            }

            VolumeViewModel? volume = State.volume;
            if (volume is null)
            {
                MessageBox.Show(
                    "Open a volume before updating volume positions.",
                    "Update Volume Positions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // The stored positions are always written under the default transform. A volume without transforms stores section
            // positions; one whose default transform did not load must not, or every aligned position would be lost.
            string? defaultTransform = volume.TransformNames.Contains(volume.DefaultVolumeTransform) ? volume.DefaultVolumeTransform : null;
            if (defaultTransform is null && volume.TransformNames.Length > 0)
            {
                MessageBox.Show(
                    $"The volume's default transform ({volume.DefaultVolumeTransform ?? "none"}) is not loaded, so volume positions cannot be updated. Reopen the volume once it loads.",
                    "Update Volume Positions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new UpdateVolumePositionsForm(defaultTransform, volume.SectionViewModels.Keys);
            if (dialog.ShowDialog(State.ViewerForm) != DialogResult.OK)
                return;

            Launch(volume, beginProgress, dialog);
        }

        private static void Launch(VolumeViewModel volume, BeginProgressDisplay beginProgress, UpdateVolumePositionsForm dialog)
        {
            var sections = new List<int>();
            if (dialog.IsAllSections)
            {
                sections.AddRange(volume.SectionViewModels.Keys);
            }
            else
            {
                foreach (long number in dialog.Sections)
                {
                    if (number < int.MinValue || number > int.MaxValue)
                        continue;
                    if (volume.SectionViewModels.ContainsKey((int)number))
                        sections.Add((int)number);
                }
            }

            if (sections.Count == 0)
            {
                MessageBox.Show(
                    "No sections to update.",
                    "Update Volume Positions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var cancellation = new CancellationTokenSource();
            (IProgress<ViewerTaskProgress> Progress, IDisposable Finish)? handle = beginProgress("Update volume positions", cancellation);
            if (handle is null)
            {
                cancellation.Dispose();
                MessageBox.Show(
                    "The status bar is already showing another task.",
                    "Update Volume Positions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _activeCancellation = cancellation;
            HookExitConfirmation(State.ViewerForm);
            _activeRun = FinishAsync(volume, sections, handle.Value.Progress, handle.Value.Finish, cancellation);
        }

        private static async Task FinishAsync(
            IDefaultVolumeTransformProvider transforms,
            List<int> sections,
            IProgress<ViewerTaskProgress> progress,
            IDisposable finish,
            CancellationTokenSource cancellation)
        {
            VolumePositionUpdateResult result;
            try
            {
                result = await Task.Run(async () =>
                    await VolumePositionUpdateJob.RunAsync(
                        transforms,
                        sections,
                        progress,
                        cancellation.Token).ConfigureAwait(false)).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                result = new VolumePositionUpdateResult(cancelled: true, error: null, corrections: null);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                result = new VolumePositionUpdateResult(cancelled: false, error: ex.Message, corrections: null);
            }
            finally
            {
                finish.Dispose();
                if (ReferenceEquals(_activeCancellation, cancellation))
                    _activeCancellation = null;
                cancellation.Dispose();
            }

            VolumePositionUpdateSummary.Show(State.ViewerForm, result);
        }
    }

    /// <summary>
    /// Scrollable list of sections that saved at least one volume-position correction.
    /// Shown on the UI thread when a pass finishes, is cancelled, or stops on a save error.
    /// </summary>
    internal static class VolumePositionUpdateSummary
    {
        public static void Show(IWin32Window? owner, VolumePositionUpdateResult result)
        {
            // Errors go to the shared dialog, which has a Copy button for error reports.
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                Viking.UI.Forms.ErrorDialog.Show(owner, "Volume position update", BuildText(result));
                return;
            }

            using Form form = new()
            {
                Text = "Volume position update",
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                ShowIcon = false,
                ShowInTaskbar = false,
                ClientSize = new System.Drawing.Size(420, 480)
            };

            var close = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Dock = DockStyle.Bottom,
                Height = 32
            };
            var list = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = BuildText(result)
            };
            form.Controls.Add(list);
            form.Controls.Add(close);
            form.AcceptButton = close;
            form.CancelButton = close;
            form.ShowDialog(owner);
        }

        private static string BuildText(VolumePositionUpdateResult result)
        {
            var text = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(result.Error))
                text.AppendLine(result.Error);
            else if (result.Cancelled)
                text.AppendLine("Cancelled. Sections already saved are listed below.");
            else
                text.AppendLine("Finished.");

            text.AppendLine();
            if (result.Corrections.Count == 0)
            {
                text.AppendLine("No corrections were made.");
                return text.ToString();
            }

            foreach (SectionCorrectionCount row in result.Corrections)
                text.AppendLine($"{row.SectionNumber} — {row.Corrections} corrections");

            return text.ToString();
        }
    }
}
