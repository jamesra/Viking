using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;
using WebAnnotation;
using WebAnnotation.ReviewFeed;

namespace Jotunn
{
    /// <summary>
    /// Viking's Review Changes tab: recent annotation changes on the volume, for users with the Review or Admin role.
    /// The feed is filled by the server backfill, the section poll and local edits (see ReviewChangeFeedIngress).
    /// </summary>
    public partial class MainWindow
    {
        async Task AddReviewFeedTabAsync()
        {
            bool hasReview;
            try
            {
                hasReview = await Task.Run(VolumeAccessRoles.HasReviewAccess).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // As in Viking, a failed check keeps the tab rather than hiding it.
                Trace.WriteLine($"Review Changes: access check failed; keeping tab. {ex.Message}");
                hasReview = true;
            }

            if (!hasReview)
                return;

            try
            {
                ReviewChangeFeedFilter.ReloadSessionFromSettings();
                ReviewChangeFeedView view = new();
                view.Attach(ReviewChangeFeed.Session);
                NavigationHost.Items.Add(new TabItem { Header = "Review Changes", Content = view });
                ReviewChangeFeedBackfill.StartAsync(ReviewChangeFeed.Session);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Review Changes: tab init failed: {ex}");
            }
        }
    }
}
