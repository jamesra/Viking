using System;
using System.Collections.Generic;
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

        /// <summary>Called from the Annotation menu click. Re-checks reviewer access before opening the dialog.</summary>
        public static async Task RunAsync()
        {
            if (Interlocked.Exchange(ref _menuBusy, 1) == 1)
                return;

            try
            {
                if (!VolumeAccessRoles.HasReviewAccess())
                    return;

                await RunCoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show(
                    "Volume position update failed.\n" + ex.Message,
                    "Update Volume Positions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Interlocked.Exchange(ref _menuBusy, 0);
            }
        }

        private static async Task RunCoreAsync()
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
            SectionViewerControl? viewer = State.ViewerControl;
            if (volume is null || viewer is null)
            {
                MessageBox.Show(
                    "Open a volume before updating volume positions.",
                    "Update Volume Positions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var dialog = new UpdateVolumePositionsForm(
                volume.TransformNames,
                string.IsNullOrEmpty(volume.ActiveVolumeTransform) ? volume.DefaultVolumeTransform : volume.ActiveVolumeTransform,
                volume.SectionViewModels.Keys);
            if (dialog.ShowDialog(State.ViewerForm) != DialogResult.OK)
                return;

            Launch(volume, viewer, dialog);
        }

        private static void Launch(VolumeViewModel volume, SectionViewerControl viewer, UpdateVolumePositionsForm dialog)
        {
            var sections = new List<VolumePositionSection>();
            if (dialog.IsAllSections)
            {
                foreach (SectionViewModel section in volume.SectionViewModels.Values)
                    sections.Add(ToTarget(section));
            }
            else
            {
                foreach (long number in dialog.Sections)
                {
                    if (number < int.MinValue || number > int.MaxValue)
                        continue;
                    if (!volume.SectionViewModels.TryGetValue((int)number, out SectionViewModel? section))
                        continue;
                    sections.Add(ToTarget(section));
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
            ViewerTaskHandle? handle = viewer.TryBeginViewerTask("Update volume positions", cancellation);
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

            MappingManager mappings = volume.CreateMappingManager();
            string? transformName = dialog.TransformName;
            _activeCancellation = cancellation;
            _activeRun = FinishAsync(mappings, sections, transformName, handle, cancellation);
        }

        private static async Task FinishAsync(
            MappingManager mappings,
            List<VolumePositionSection> sections,
            string? transformName,
            ViewerTaskHandle handle,
            CancellationTokenSource cancellation)
        {
            VolumePositionUpdateResult result;
            try
            {
                result = await Task.Run(async () =>
                    await VolumePositionUpdateJob.RunAsync(
                        mappings,
                        sections,
                        transformName,
                        handle.Progress,
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
                handle.Dispose();
                if (ReferenceEquals(_activeCancellation, cancellation))
                    _activeCancellation = null;
                cancellation.Dispose();
            }

            VolumePositionUpdateSummary.Show(State.ViewerForm, result);
        }

        private static VolumePositionSection ToTarget(SectionViewModel section)
        {
            return new VolumePositionSection(section.Number, section.DefaultChannel, section.DefaultPyramidTransform);
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
