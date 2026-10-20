using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Viking.Common;
using WebAnnotation;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Left ACTION module tab listing recent visible-section annotation changes for Review users.
    /// Registered like Structure Types; removed at load when the session has no Review access.
    /// Construction is deferred to a background task so volume startup is not blocked.
    /// </summary>
    [ExtensionTab("Review Changes", TABCATEGORY.ACTION)]
    public class ReviewChangeFeedTab : Viking.UI.BaseClasses.DockableUserControl
    {
        ElementHost _host;
        ReviewChangeFeedView _view;
        bool _removedForAccess;
        bool _initStarted;

        /// <summary>
        /// Lightweight ctor only. WPF host, attach, and backfill run after Load on a task.
        /// </summary>
        public ReviewChangeFeedTab()
        {
            Title = "Review Changes";
            Load += OnLoaded;
        }

        void OnLoaded(object sender, EventArgs e)
        {
            if (_initStarted || _removedForAccess)
                return;
            _initStarted = true;

            // Leave the Load handler immediately; init after the current UI message finishes.
            BeginInvoke(new Action(() => _ = InitializeAsync()));
        }

        /// <summary>
        /// Access check and backfill on the thread pool; ElementHost creation on the UI thread.
        /// Access-check failures keep the tab (do not treat exceptions as "no Review").
        /// </summary>
        async Task InitializeAsync()
        {
            bool hasReview;
            try
            {
                hasReview = await Task.Run(VolumeAccessRoles.HasReviewAccess).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(
                    $"Review Changes: access check failed; keeping tab. {ex.Message}",
                    "WebAnnotation");
                hasReview = true;
            }

            if (IsDisposed)
                return;

            if (!hasReview)
            {
                Trace.WriteLine(
                    "Review Changes: session has no Review/Admin role; removing ACTION tab.",
                    "WebAnnotation");
                RemoveOwnTabPage();
                _removedForAccess = true;
                return;
            }

            try
            {
                // Reload after Upgrade so Velopack version folders keep the last Apply.
                ReviewChangeFeedFilter.ReloadSessionFromSettings();
                EnsureHostCreated();
                ReviewChangeFeedBackfill.StartAsync(ReviewChangeFeed.Session);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Review Changes: host init failed: {ex}", "WebAnnotation");
            }
        }

        void EnsureHostCreated()
        {
            if (_host != null || IsDisposed)
                return;

            _view = new ReviewChangeFeedView();
            _view.Attach(ReviewChangeFeed.Session);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _view
            };
            Controls.Add(_host);
        }

        /// <summary>
        /// Drops this control's TabPage when the user lacks Review. Called once at load.
        /// </summary>
        void RemoveOwnTabPage()
        {
            if (Parent is not TabPage page)
                return;
            if (page.Parent is not TabControl tabs)
                return;
            tabs.TabPages.Remove(page);
        }
    }
}
