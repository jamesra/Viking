using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velopack;
using NuGet.Versioning;

namespace Viking.Services
{
    /// <summary>
    /// Service for managing application updates using Velopack.
    /// Handles checking for updates, downloading, and applying updates.
    /// </summary>
    public static class UpdateService
    {
        private const string UpdateUrl = "https://websvc.codepharm.net/Software/Viking";

        /// <summary>
        /// Shows a "Checking for updates" form and checks for updates at startup.
        /// This runs on the UI thread before the login dialog is shown.
        /// Uses a proper message loop to keep the form responsive during async operations.
        /// </summary>
        public static void CheckForUpdatesAtStartup()
        {
            // Show "Checking for updates" form
            Form? checkingForm = null;

            var mgr = new UpdateManager(UpdateUrl);

            try
            {
                // Create and show checking form using helper
                checkingForm = CreateSimpleDialog("Checking for Updates", "Checking for updates...");
                checkingForm.Show();
                checkingForm.Refresh();
                Application.DoEvents(); // Ensure form is visible

                // Check for updates and handle download if available
                var (release, version) = CheckAndHandleUpdates(mgr);

                // Close the checking form
                SafeDisposeForm(checkingForm);
                checkingForm = null;
                Application.DoEvents(); // Process any pending messages

                // If update was downloaded, apply and restart
                if (release != null && version != null)
                {
                    ApplyUpdateAndRestart(mgr, release, version);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Velopack] Error during update check at startup: {ex.Message}");
                SafeDisposeForm(checkingForm);
                // Continue to login even if update check fails
            }
        }

        /// <summary>
        /// Checks for available updates and handles downloading/installing if found.
        /// Returns tuple with release and version, or (null, null) if no update or user declines.
        /// </summary>
        private static (VelopackAsset? release, SemanticVersion? version) CheckAndHandleUpdates(UpdateManager mgr)
        {
            try
            {
                var updateInfo = mgr.CheckForUpdates();

                if (updateInfo == null)
                {
                    Trace.WriteLine("[Velopack] No updates available - running latest version");
                    return (null, null);
                }

                var currentVersion = mgr.CurrentVersion;
                var newVersion = updateInfo.TargetFullRelease.Version;
                var newVersionString = newVersion.ToString();

                Trace.WriteLine($"[Velopack] Update available: {currentVersion} -> {newVersionString}");

                // Notify user about available update and prompt to download
                var result = MessageBox.Show(
                    $"A new version of Viking ({newVersionString}) is available.\n\n" +
                    $"Current version: {currentVersion}\n\n" +
                    "Would you like to download and install it now?\n\n" +
                    "Note: The application will close and restart after the update.",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    // Create download form on UI thread (we're already on UI thread at startup)
                    Form? downloadForm = null;
                    ProgressBar? progressBar = null;
                    Label? statusLabel = null;

                    try
                    {
                        // Create download progress dialog using helper
                        (downloadForm, progressBar, statusLabel) = CreateDownloadDialog(newVersionString);
                        downloadForm.Show();
                        downloadForm.Refresh();
                        Application.DoEvents(); // Ensure form is visible before starting download

                        // Download the update with progress callback
                        // Note: We're in a synchronous context, so we use DoEvents() loop to keep UI responsive
                        // The callback updates UI on the UI thread (we check InvokeRequired for safety)
                        var downloadTask = mgr.DownloadUpdatesAsync(updateInfo, (progress) =>
                        {
                            // Ensure UI updates happen on UI thread (they should already, but check for safety)
                            if (downloadForm != null && !downloadForm.IsDisposed)
                            {
                                if (downloadForm.InvokeRequired)
                                {
                                    downloadForm.Invoke(new Action(() =>
                                    {
                                        if (progressBar != null && !progressBar.IsDisposed)
                                            progressBar.Value = (int)progress;
                                        if (statusLabel != null && !statusLabel.IsDisposed)
                                            statusLabel.Text = $"Downloading version {newVersionString}... {progress}%";
                                        Application.DoEvents();
                                    }));
                                }
                                else
                                {
                                    progressBar!.Value = (int)progress;
                                    statusLabel!.Text = $"Downloading version {newVersionString}... {progress}%";
                                    Application.DoEvents();
                                }
                            }
                        });

                        // Keep UI responsive while downloading (necessary in synchronous context before Application.Run)
                        // Properly await the task with UI message pumping
                        while (!downloadTask.IsCompleted)
                        {
                            Application.DoEvents();
                            Thread.Sleep(50); // Small sleep to avoid 100% CPU
                        }

                        // Wait for task completion to catch any exceptions
                        // Use GetAwaiter().GetResult() here since we're in a synchronous method and already pumping messages
                        downloadTask.GetAwaiter().GetResult();

                        // Update status to show download complete
                        if (downloadForm != null && !downloadForm.IsDisposed)
                        {
                            statusLabel!.Text = "Download complete. Preparing to apply update...";
                            progressBar!.Value = 100;
                            Application.DoEvents();
                        }

                        Trace.WriteLine($"[Velopack] Update downloaded successfully. Version {newVersionString} ready to install.");

                        // Close and dispose the download form
                        SafeDisposeForm(downloadForm);
                        downloadForm = null; // Clear reference

                        // Brief pause to ensure form is fully disposed before proceeding
                        Application.DoEvents();
                        Thread.Sleep(100);

                        return (updateInfo.TargetFullRelease, newVersion);
                    }
                    catch (Exception downloadEx)
                    {
                        SafeDisposeForm(downloadForm);

                        var errorMessage = $"Error downloading update:\n\n{downloadEx.Message}";
                        if (downloadEx.StackTrace != null)
                        {
                            errorMessage += $"\n\nStack trace:\n{downloadEx.StackTrace}";
                        }

                        MessageBox.Show(
                            errorMessage,
                            "Update Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        Trace.WriteLine($"[Velopack] Error downloading update: {downloadEx.Message}");
                        if (downloadEx.StackTrace != null)
                        {
                            Trace.WriteLine($"[Velopack] Stack trace: {downloadEx.StackTrace}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently handle update check failures - don't disrupt user experience
                Trace.WriteLine($"[Velopack] Error checking for updates: {ex.Message}");
            }

            return (null, null);
        }

        /// <summary>
        /// Applies the downloaded update and restarts the application.
        /// This method must be called on the UI thread.
        /// </summary>
        private static void ApplyUpdateAndRestart(UpdateManager mgr, VelopackAsset release, SemanticVersion version)
        {
            try
            {
                // Save all application settings before applying update
                try
                {
                    Properties.Settings.Default.Save();
                    Trace.WriteLine("[Velopack] Application settings saved before update.");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[Velopack] Warning: Failed to save settings before update: {ex.Message}");
                }

                var versionString = version.ToString();
                Trace.WriteLine($"[Velopack] Calling ApplyUpdatesAndRestart for version {versionString}...");

                // Close all open forms gracefully before applying update
                // Velopack's ApplyUpdatesAndRestart will handle the exit, but we close forms to ensure clean state
                var forms = new List<Form>();
                foreach (Form form in Application.OpenForms)
                {
                    if (form != null && !form.IsDisposed)
                    {
                        forms.Add(form);
                    }
                }

                // Close forms in reverse order (top-most first)
                for (int i = forms.Count - 1; i >= 0; i--)
                {
                    SafeDisposeForm(forms[i]);
                }

                Application.DoEvents();

                // Apply updates and restart the application
                // This method replaces files and restarts the application
                // It does NOT return normally - it launches the updated app and exits the current one
                mgr.ApplyUpdatesAndRestart(release);

                // If we reach here, ApplyUpdatesAndRestart didn't work as expected
                // This should not happen, but handle it gracefully
                Trace.WriteLine($"[Velopack] Warning: ApplyUpdatesAndRestart returned unexpectedly. Forcing exit...");
                Application.Exit();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Velopack] Error applying update: {ex.Message}");
                if (ex.StackTrace != null)
                {
                    Trace.WriteLine($"[Velopack] Stack trace: {ex.StackTrace}");
                }

                MessageBox.Show(
                    $"Error applying update:\n\n{ex.Message}\n\n" +
                    "Please restart the application manually. The update will be applied on the next launch.",
                    "Update Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// Creates a simple modal dialog with a message.
        /// All operations occur on the UI thread.
        /// </summary>
        private static Form CreateSimpleDialog(string title, string message, int width = 350, int height = 120)
        {
            var form = new Form()
            {
                Text = title,
                Width = width,
                Height = height,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                AutoSize = false,
                AutoScaleMode = AutoScaleMode.Font,
                Padding = new Padding(20)
            };

            var label = new Label()
            {
                Text = message,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                AutoSize = false,
                Padding = new Padding(10)
            };

            form.Controls.Add(label);
            return form;
        }

        /// <summary>
        /// Creates a download progress dialog with progress bar and status label.
        /// All operations occur on the UI thread.
        /// </summary>
        private static (Form form, ProgressBar progressBar, Label statusLabel) CreateDownloadDialog(string version)
        {
            var form = new Form()
            {
                Text = "Downloading Update",
                Width = 450,
                Height = 200,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                AutoSize = false,
                AutoScaleMode = AutoScaleMode.Font,
                Padding = new Padding(20)
            };

            var tableLayout = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

            var statusLabel = new Label()
            {
                Text = $"Downloading version {version}...",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                AutoSize = false,
                AutoEllipsis = true,
                Padding = new Padding(10)
            };

            var progressBar = new ProgressBar()
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Margin = new Padding(10, 5, 10, 5)
            };

            tableLayout.Controls.Add(statusLabel, 0, 0);
            tableLayout.Controls.Add(progressBar, 0, 1);
            form.Controls.Add(tableLayout);

            return (form, progressBar, statusLabel);
        }

        /// <summary>
        /// Safely disposes a form, ensuring it's closed and disposed on the UI thread.
        /// </summary>
        private static void SafeDisposeForm(Form? form)
        {
            if (form == null || form.IsDisposed)
                return;

            try
            {
                if (form.InvokeRequired)
                {
                    form.Invoke(new Action(() =>
                    {
                        try
                        {
                            form.Close();
                            form.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"[UpdateService] Error disposing form: {ex.Message}");
                        }
                    }));
                }
                else
                {
                    form.Close();
                    form.Dispose();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[UpdateService] Error disposing form: {ex.Message}");
            }
        }
    }
}
