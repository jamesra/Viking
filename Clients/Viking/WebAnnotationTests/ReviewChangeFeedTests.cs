using System;
using System.Drawing;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;
using WebAnnotation.ReviewFeed;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pure tests for Review feed coalesce, mask fill, and the Review gate helper.
    /// </summary>
    [TestClass]
    public class ReviewChangeFeedTests
    {
        static ReviewChangeEntry Entry(long id, DateTime modified, int section = 1, string username = "alice") =>
            new(
                id,
                structureId: 10,
                structureLabel: "c1",
                typeName: "GAC",
                section,
                modified,
                username,
                centerX: 100,
                centerY: 200,
                minX: 90,
                minY: 190,
                maxX: 110,
                maxY: 210,
                shapeRing: new[]
                {
                    new Vector2(90, 190),
                    new Vector2(110, 190),
                    new Vector2(110, 210),
                    new Vector2(90, 210)
                },
                ReviewChangeCoordinateSpace.Volume,
                isDeleted: false);

        [TestMethod]
        public void UsernameLabel_AndTimeLabel_OnNonDeletedCards()
        {
            DateTime utc = new(2020, 10, 8, 21, 32, 0, DateTimeKind.Utc);
            ReviewChangeEntry entry = Entry(42, utc, username: "bob");
            Assert.AreEqual("bob", entry.UsernameLabel);
            StringAssert.Contains(entry.Caption, "#42");
            Assert.IsFalse(entry.Caption.Contains("bob"), "Username is a separate card line");
            DateTime local = utc.ToLocalTime();
            Assert.AreEqual(local.ToString("MM/dd/yyyy HH:mm"), entry.TimeLabel);
            StringAssert.Contains(entry.TimeLabel, local.Year.ToString());
        }

        [TestMethod]
        public void DeletedRow_IsSingleLineWithoutImageFields()
        {
            DateTime utc = new(2020, 10, 8, 21, 32, 0, DateTimeKind.Utc);
            ReviewChangeEntry entry = ReviewChangeEntry.Deleted(99, utc);
            StringAssert.Contains(entry.DeletedRowText, "deleted");
            StringAssert.Contains(entry.DeletedRowText, "#99");
            StringAssert.Contains(entry.DeletedRowText, utc.ToLocalTime().Year.ToString());
            Assert.IsTrue(entry.IsDeleted);
        }

        [TestMethod]
        public void ApplyUpserts_SortsNewestToOldest_RegardlessOfArrivalOrder()
        {
            var feed = new ReviewChangeFeed(maxEntries: 10);
            DateTime t0 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            feed.ApplyUpserts([
                Entry(1, t0.AddMinutes(30)),
                Entry(2, t0.AddMinutes(5)),
                Entry(3, t0.AddMinutes(50)),
            ]);

            Assert.AreEqual(3L, feed.Entries[0].LocationId);
            Assert.AreEqual(1L, feed.Entries[1].LocationId);
            Assert.AreEqual(2L, feed.Entries[2].LocationId);
        }

        [TestMethod]
        public void ApplyUpserts_OlderArrivalDoesNotJumpAheadOfNewer()
        {
            var feed = new ReviewChangeFeed(maxEntries: 10);
            DateTime t0 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            feed.ApplyUpserts([Entry(1, t0.AddMinutes(30))]);
            feed.ApplyUpserts([Entry(2, t0.AddMinutes(5))]);

            Assert.AreEqual(1L, feed.Entries[0].LocationId);
            Assert.AreEqual(2L, feed.Entries[1].LocationId);
        }

        [TestMethod]
        public void ApplyUpserts_TrimDropsOldestByTime()
        {
            var feed = new ReviewChangeFeed(maxEntries: 2);
            DateTime t0 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            feed.ApplyUpserts([
                Entry(1, t0.AddMinutes(1)),
                Entry(2, t0.AddMinutes(10)),
                Entry(3, t0.AddMinutes(5)),
            ]);

            Assert.AreEqual(2, feed.Count);
            Assert.AreEqual(2L, feed.Entries[0].LocationId);
            Assert.AreEqual(3L, feed.Entries[1].LocationId);
        }

        [TestMethod]
        public void ODataRoot_ReplacesAnnotationSegment_OnAnnotationHost()
        {
            Assert.IsTrue(ReviewChangeODataQuery.TryODataRoot(
                "https://webdev.connectomes.utah.edu/RC1Test/Annotation/Service.svc",
                out Uri root));
            Assert.AreEqual("https://webdev.connectomes.utah.edu/RC1Test/OData/", root.AbsoluteUri);

            Assert.IsTrue(ReviewChangeODataQuery.TryODataRoot(
                "https://rouge1.codepharm.net/RABBIT/annotation/service.svc",
                out Uri lower));
            Assert.AreEqual("https://rouge1.codepharm.net/RABBIT/OData/", lower.AbsoluteUri);

            Assert.IsTrue(ReviewChangeODataQuery.TryODataRoot(root.AbsoluteUri, out Uri already));
            Assert.AreEqual(root.AbsoluteUri, already.AbsoluteUri);

            Assert.IsFalse(ReviewChangeODataQuery.TryODataRoot(
                "http://websvc1.connectomes.utah.edu/RC1/Volume.VikingXML",
                out _));
        }

        [TestMethod]
        public void RecentLocationsUri_OrdersByLastModified_AndTakesTop()
        {
            Assert.IsTrue(ReviewChangeODataQuery.TryODataRoot(
                "http://example.test/RC1/Annotation/Service.svc",
                out Uri root));

            Uri query = ReviewChangeODataQuery.BuildRecentLocationsUri(root, 50, sections: null);
            string decoded = Uri.UnescapeDataString(query.Query);

            StringAssert.Contains(decoded, "$orderby=LastModified desc");
            StringAssert.Contains(decoded, "$top=50");
            Assert.IsFalse(decoded.Contains("$filter="));
        }

        [TestMethod]
        public void SectionFilter_CollapsesAdjacentZ_AndQueryIncludesIt()
        {
            string filter = ReviewChangeODataQuery.BuildSectionFilter(new long[] { 3, 1, 2, 5, 5 });
            Assert.AreEqual("(Z ge 1 and Z le 3) or Z eq 5", filter);

            Assert.IsTrue(ReviewChangeODataQuery.TryODataRoot(
                "http://example.test/RC1/Annotation/Service.svc",
                out Uri root));
            Uri query = ReviewChangeODataQuery.BuildRecentLocationsUri(root, 50, new long[] { 1, 2, 3, 5 });
            string decoded = Uri.UnescapeDataString(query.Query);
            StringAssert.Contains(decoded, "$filter=(" + filter + ")");
        }

        [TestMethod]
        public void ODataFilter_IncludesUsernameAndStructure()
        {
            string combined = ReviewChangeODataQuery.BuildFilter(
                sections: new long[] { 10 },
                usernames: new[] { "gnk19pitt", "JamesAN" },
                structureOrLabel: "c1");

            StringAssert.Contains(combined, "Z eq 10");
            StringAssert.Contains(combined, "tolower(Username) eq 'gnk19pitt'");
            StringAssert.Contains(combined, "tolower(Username) eq 'jamesan'");
            StringAssert.Contains(combined, "contains(tolower(Parent/Label),'c1')");

            Assert.IsTrue(ReviewChangeODataQuery.TryODataRoot(
                "http://example.test/RC1/Annotation/Service.svc",
                out Uri root));
            Uri query = ReviewChangeODataQuery.BuildRecentLocationsUri(
                root, 50, sections: null, usernames: new[] { "gnk19pitt" }, structureOrLabel: null);
            string decoded = Uri.UnescapeDataString(query.Query);
            StringAssert.Contains(decoded, "tolower(Username) eq 'gnk19pitt'");
        }

        [TestMethod]
        public void SeedRecent_ReplacesPriorRows_WhenFilterChanges()
        {
            var feed = new ReviewChangeFeed(maxEntries: 10);
            DateTime t0 = new(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            feed.SeedRecent([Entry(1, t0, username: "alice")]);
            Assert.AreEqual(1, feed.Count);

            feed.SeedRecent([Entry(2, t0.AddMinutes(1), username: "bob")]);
            Assert.AreEqual(1, feed.Count);
            Assert.AreEqual(2L, feed.Entries[0].LocationId);

            feed.SeedRecent(Array.Empty<ReviewChangeEntry>());
            Assert.AreEqual(0, feed.Count);
        }

        [TestMethod]
        public void ParseLocations_ReadsNewestRow_WithYearAndShape()
        {
            const string json = """
                {
                  "value": [
                    {
                      "ID": 7,
                      "ParentID": 10,
                      "Z": 3,
                      "X": 1,
                      "Y": 2,
                      "VolumeX": 100,
                      "VolumeY": 200,
                      "Radius": 4,
                      "LastModified": "2026-10-09T18:32:00Z",
                      "Username": "bob",
                      "VolumeShape": {
                        "Geometry": {
                          "CoordinateSystemId": 0,
                          "WellKnownText": "POLYGON ((90 190, 110 190, 110 210, 90 210, 90 190))"
                        }
                      },
                      "MosaicShape": {
                        "Geometry": {
                          "CoordinateSystemId": 0,
                          "WellKnownText": "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))"
                        }
                      },
                      "Parent": { "ID": 10, "Label": "c1", "Type": { "Name": "GAC", "Code": "G" } }
                    }
                  ]
                }
                """;

            var entries = ReviewChangeODataQuery.ParseLocations(json);
            Assert.AreEqual(1, entries.Count);
            ReviewChangeEntry entry = entries[0];
            Assert.AreEqual(7L, entry.LocationId);
            Assert.AreEqual(10L, entry.StructureId);
            Assert.AreEqual(3, entry.Section);
            Assert.AreEqual("bob", entry.UsernameLabel);
            StringAssert.Contains(entry.Caption, "GAC");
            StringAssert.Contains(entry.Caption, "c1");
            Assert.AreEqual(new DateTime(2026, 10, 9, 18, 32, 0, DateTimeKind.Utc), entry.LastModifiedUtc);
            StringAssert.Contains(entry.TimeLabel, entry.LastModifiedUtc.ToLocalTime().Year.ToString());
            Assert.AreEqual(ReviewChangeCoordinateSpace.Volume, entry.CoordinateSpace);
            Assert.IsTrue(entry.ShapeRing.Count >= 4);
            Assert.AreEqual(90, entry.MinX, 0.001);
            Assert.IsTrue(entry.BBoxWidth > 0);
        }

        [TestMethod]
        public void CardCrop_WorldToImage_MatchesMaskAndEmMapping()
        {
            ReviewChangeCardCrop.GetPaddedBounds(90, 190, 110, 210, out double left, out double bottom, out double spanX, out double spanY);
            Assert.IsTrue(left < 90);
            Assert.IsTrue(bottom < 190);

            ReviewChangeCardCrop.WorldToImage(100, 200, left, bottom, spanX, spanY, 256, out float x, out float y);
            Assert.IsTrue(x > 0 && x < 256);
            Assert.IsTrue(y > 0 && y < 256);
        }

        [TestMethod]
        public void SeedRecent_SetsPollCutoffToOldestSeededStamp()
        {
            var feed = new ReviewChangeFeed(maxEntries: 10);
            DateTime t0 = new(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            feed.SeedRecent([
                Entry(3, t0.AddMinutes(30)),
                Entry(2, t0.AddMinutes(10)),
                Entry(1, t0.AddMinutes(5)),
            ]);

            Assert.AreEqual(t0.AddMinutes(5), feed.PollIngressCutoffUtc);
            Assert.IsTrue(feed.AllowsPollArrival(t0.AddMinutes(5)));
            Assert.IsTrue(feed.AllowsPollArrival(t0.AddMinutes(40)));
            Assert.IsFalse(feed.AllowsPollArrival(t0.AddMinutes(4)));
        }

        [TestMethod]
        public void AllowsPollArrival_UsesCutoff_OrDefaultLookbackWhenUnset()
        {
            var feed = new ReviewChangeFeed();
            DateTime t0 = new(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            feed.SetPollIngressCutoffUtc(t0.AddMinutes(20));

            Assert.IsFalse(feed.AllowsPollArrival(t0.AddMinutes(10)));
            Assert.IsTrue(feed.AllowsPollArrival(t0.AddMinutes(25)));

            var unset = new ReviewChangeFeed();
            Assert.IsTrue(unset.AllowsPollArrival(DateTime.UtcNow));
            Assert.IsFalse(unset.AllowsPollArrival(DateTime.UtcNow - ReviewChangeFeed.DefaultLookback - TimeSpan.FromMinutes(1)));
        }

        [TestMethod]
        public void SetSeedStatus_RaisesChanged_AndClearsOnSuccessfulSeed()
        {
            var feed = new ReviewChangeFeed();
            int changes = 0;
            feed.Changed += (_, _) => changes++;

            feed.SetSeedStatus("OData recent-locations request failed: timeout");
            Assert.AreEqual("OData recent-locations request failed: timeout", feed.SeedStatusMessage);
            Assert.IsTrue(changes >= 1);

            feed.SeedRecent([Entry(1, DateTime.UtcNow)]);
            Assert.AreEqual("", feed.SeedStatusMessage);
        }

        [TestMethod]
        public void ChooseDownsampleLevel_PicksCoarsestFineEnough()
        {
            int level = ReviewChangeMosaicCropLoader.ChooseDownsampleLevel(
                new[] { 1, 2, 4, 8 },
                worldSpan: 1000,
                targetPixels: 256);
            // ideal ds ≈ 1000/256 ≈ 3.9 → coarsest level >= 3.9 is 4
            Assert.AreEqual(4, level);
        }

        [TestMethod]
        public void ApplyUpserts_CoalescesById_KeepsNewest_MovesToTop()
        {
            var feed = new ReviewChangeFeed(maxEntries: 10);
            DateTime older = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime newer = older.AddHours(1);

            feed.ApplyUpserts([Entry(1, older), Entry(2, older)]);
            feed.ApplyUpserts([Entry(1, newer)]);

            Assert.AreEqual(2, feed.Count);
            Assert.AreEqual(1L, feed.Entries[0].LocationId);
            Assert.AreEqual(newer, feed.Entries[0].LastModifiedUtc);
            Assert.AreEqual(2L, feed.Entries[1].LocationId);
        }

        [TestMethod]
        public void ApplyUpserts_IgnoresOlderStampForExistingId()
        {
            var feed = new ReviewChangeFeed(maxEntries: 10);
            DateTime newer = new(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            DateTime older = newer.AddHours(-1);

            feed.ApplyUpserts([Entry(5, newer)]);
            feed.ApplyUpserts([Entry(5, older)]);

            Assert.AreEqual(1, feed.Count);
            Assert.AreEqual(newer, feed.Entries[0].LastModifiedUtc);
        }

        [TestMethod]
        public void ApplyUpserts_IgnoresEqualStamp_DoesNotRaiseChanged()
        {
            var feed = new ReviewChangeFeed(maxEntries: 10);
            DateTime t0 = new(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            feed.ApplyUpserts([Entry(5, t0)]);

            int changes = 0;
            feed.Changed += (_, _) => changes++;
            feed.ApplyUpserts([Entry(5, t0)]);

            Assert.AreEqual(0, changes);
            Assert.AreEqual(1, feed.Count);
        }

        [TestMethod]
        public void ApplyUpserts_TrimsToMaxEntries()
        {
            var feed = new ReviewChangeFeed(maxEntries: 3);
            DateTime t0 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            feed.ApplyUpserts([
                Entry(1, t0),
                Entry(2, t0.AddMinutes(1)),
                Entry(3, t0.AddMinutes(2)),
                Entry(4, t0.AddMinutes(3))
            ]);

            Assert.AreEqual(3, feed.Count);
            Assert.AreEqual(4L, feed.Entries[0].LocationId);
            Assert.AreEqual(3L, feed.Entries[1].LocationId);
            Assert.AreEqual(2L, feed.Entries[2].LocationId);
        }

        [TestMethod]
        public void ApplyDeletes_InsertsDeletedRowAtTop()
        {
            var feed = new ReviewChangeFeed();
            DateTime t0 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            feed.ApplyUpserts([Entry(9, t0)]);
            feed.ApplyDeletes([9], t0.AddMinutes(5));

            Assert.AreEqual(1, feed.Count);
            Assert.IsTrue(feed.Entries[0].IsDeleted);
            Assert.AreEqual(9L, feed.Entries[0].LocationId);
        }

        [TestMethod]
        public void SelectRecentWindow_DropsOlderThanLookback_CapsAtMax()
        {
            DateTime now = new(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var candidates = new[]
            {
                Entry(1, now.AddMinutes(-10)),
                Entry(2, now.AddMinutes(-30)),
                Entry(3, now.AddMinutes(-70)),
                Entry(4, now.AddMinutes(-5)),
                Entry(5, now.AddMinutes(-20)),
                Entry(6, now.AddMinutes(-40)),
            };

            var selected = ReviewChangeFeed.SelectRecentWindow(
                candidates,
                now,
                TimeSpan.FromHours(1),
                maxCount: 3);

            Assert.AreEqual(3, selected.Count);
            Assert.AreEqual(4L, selected[0].LocationId);
            Assert.AreEqual(1L, selected[1].LocationId);
            Assert.AreEqual(5L, selected[2].LocationId);
        }

        [TestMethod]
        public void SelectRecentWindow_CoalescesDuplicateIds_KeepsNewest()
        {
            DateTime now = new(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var candidates = new[]
            {
                Entry(1, now.AddMinutes(-40)),
                Entry(1, now.AddMinutes(-5)),
                Entry(2, now.AddMinutes(-10)),
            };

            var selected = ReviewChangeFeed.SelectRecentWindow(
                candidates,
                now,
                TimeSpan.FromHours(1),
                maxCount: 50);

            Assert.AreEqual(2, selected.Count);
            Assert.AreEqual(1L, selected[0].LocationId);
            Assert.AreEqual(now.AddMinutes(-5), selected[0].LastModifiedUtc);
        }

        [TestMethod]
        public void SeedRecent_AppliesOldestFirst_NewestEndsOnTop()
        {
            var feed = new ReviewChangeFeed(maxEntries: 10);
            DateTime t0 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var newestFirst = new[]
            {
                Entry(3, t0.AddMinutes(3)),
                Entry(2, t0.AddMinutes(2)),
                Entry(1, t0.AddMinutes(1)),
            };

            feed.SeedRecent(newestFirst);

            Assert.AreEqual(3, feed.Count);
            Assert.AreEqual(3L, feed.Entries[0].LocationId);
            Assert.AreEqual(2L, feed.Entries[1].LocationId);
            Assert.AreEqual(1L, feed.Entries[2].LocationId);
        }

        [TestMethod]
        public void RasterizeMask_FillsInterior_LeavesOutsideClear()
        {
            var ring = new[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10)
            };

            bool[,] mask = ReviewChangeMaskComposer.RasterizeMask(ring, 0, 0, 10, 10, size: 32);

            Assert.IsTrue(mask[16, 16], "Center of square should be set");
            Assert.IsFalse(mask[0, 0], "Far corner after padding should be clear");
        }

        [TestMethod]
        public void TintMaskedPixels_BlendsOnlyWhereMaskIsSet()
        {
            const int size = 4;
            var mask = new bool[size, size];
            mask[1, 1] = true;
            var pixels = new byte[size * size * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 255;
            }

            ReviewChangeMaskComposer.TintMaskedPixels(
                pixels,
                size,
                size,
                mask,
                Color.FromArgb(255, 255, 0, 0),
                visibility: 1f,
                clearOutside: false);

            int set = (1 * size + 1) * 4;
            Assert.AreEqual(255, pixels[set + 2], "Tinted pixel red channel");
            int clear = 0;
            Assert.AreEqual(0, pixels[clear + 2], "Unset pixel stays black");
        }

        [TestMethod]
        public void Compose_ReturnsBitmapOfRequestedSize()
        {
            ReviewChangeEntry entry = Entry(1, DateTime.UtcNow);
            using Bitmap bitmap = ReviewChangeMaskComposer.Compose(entry, size: 64);
            Assert.AreEqual(64, bitmap.Width);
            Assert.AreEqual(64, bitmap.Height);
        }

        [TestMethod]
        public void ComputeCardThumbSize_TracksListWidthMinusChrome()
        {
            Assert.AreEqual(256, ReviewChangeCardLayout.ComputeCardThumbSize(300, verticalScrollBarWidth: 28));
            Assert.AreEqual(184, ReviewChangeCardLayout.ComputeCardThumbSize(200, verticalScrollBarWidth: 0));
            Assert.AreEqual(48, ReviewChangeCardLayout.ComputeCardThumbSize(40, verticalScrollBarWidth: 0));
            Assert.AreEqual(48, ReviewChangeCardLayout.ComputeCardThumbSize(0, verticalScrollBarWidth: 0));
        }

        [TestMethod]
        public void StructureOrLabelMatch_MatchesIdOrLabelSubstring()
        {
            Assert.IsTrue(StructureOrLabelMatch.Matches(10, "c1", "10"));
            Assert.IsTrue(StructureOrLabelMatch.Matches(10, "c1", "c1"));
            Assert.IsTrue(StructureOrLabelMatch.Matches(105, "axon", "10"));
            Assert.IsFalse(StructureOrLabelMatch.Matches(10, "c1", "bob"));
            Assert.IsTrue(StructureOrLabelMatch.Matches(10, "c1", ""));
        }

        [TestMethod]
        public void Filter_HideOwn_ExcludesCurrentUser()
        {
            var filter = new ReviewChangeFeedFilter();
            filter.Apply(hideOwnChanges: true, watchedUsersText: "", structureOrLabelText: "", persist: false);

            Assert.IsFalse(filter.Allows(Entry(1, DateTime.UtcNow, username: "alice"), "alice"));
            Assert.IsTrue(filter.Allows(Entry(2, DateTime.UtcNow, username: "bob"), "alice"));
        }

        [TestMethod]
        public void Filter_WatchedUsers_OnlyThoseNames()
        {
            var filter = new ReviewChangeFeedFilter();
            filter.Apply(hideOwnChanges: false, watchedUsersText: "bob, carol", structureOrLabelText: "", persist: false);

            Assert.IsFalse(filter.Allows(Entry(1, DateTime.UtcNow, username: "alice"), "alice"));
            Assert.IsTrue(filter.Allows(Entry(2, DateTime.UtcNow, username: "bob"), "alice"));
        }

        [TestMethod]
        public void Filter_StructureOrLabel_RestrictsRows()
        {
            var filter = new ReviewChangeFeedFilter();
            filter.Apply(hideOwnChanges: false, watchedUsersText: "", structureOrLabelText: "c1", persist: false);

            Assert.IsTrue(filter.Allows(Entry(1, DateTime.UtcNow, username: "bob"), "alice"));
            var other = new ReviewChangeEntry(
                99, 99, "other", "GAC", 1, DateTime.UtcNow, "bob",
                0, 0, 0, 0, 1, 1, Array.Empty<Vector2>(),
                ReviewChangeCoordinateSpace.Volume, false);
            Assert.IsFalse(filter.Allows(other, "alice"));
        }

        [TestMethod]
        public void Filter_BlankSections_MeansAllSections()
        {
            var filter = new ReviewChangeFeedFilter();
            filter.Apply(
                hideOwnChanges: false,
                watchedUsersText: "",
                structureOrLabelText: "",
                sectionsText: "",
                persist: false);

            Assert.IsTrue(filter.IsAllSections);
            Assert.IsTrue(filter.Allows(Entry(1, DateTime.UtcNow, section: 50, username: "bob"), "alice"));
            Assert.IsFalse(filter.Allows(ReviewChangeEntry.Deleted(9, DateTime.UtcNow), "alice"));
        }

        [TestMethod]
        public void Filter_HideDeleted_DefaultsToChecked_AndCanBeTurnedOff()
        {
            var filter = new ReviewChangeFeedFilter();
            Assert.IsTrue(filter.HideDeleted);
            Assert.IsFalse(filter.Allows(ReviewChangeEntry.Deleted(9, DateTime.UtcNow), "alice"));
            Assert.IsTrue(filter.Allows(Entry(1, DateTime.UtcNow, username: "bob"), "alice"));

            filter.Apply(
                hideOwnChanges: false,
                watchedUsersText: "",
                structureOrLabelText: "",
                sectionsText: "",
                persist: false,
                hideDeleted: false);

            Assert.IsFalse(filter.HideDeleted);
            Assert.IsTrue(filter.Allows(ReviewChangeEntry.Deleted(9, DateTime.UtcNow), "alice"));
        }

        [TestMethod]
        public void Filter_SectionsRange_KeepsOnlyMatchingSections()
        {
            var filter = new ReviewChangeFeedFilter();
            filter.Apply(
                hideOwnChanges: false,
                watchedUsersText: "",
                structureOrLabelText: "",
                sectionsText: "10-12, 20",
                persist: false,
                hideDeleted: false);

            Assert.IsFalse(filter.IsAllSections);
            Assert.IsTrue(filter.Allows(Entry(1, DateTime.UtcNow, section: 11, username: "bob"), "alice"));
            Assert.IsTrue(filter.Allows(Entry(2, DateTime.UtcNow, section: 20, username: "bob"), "alice"));
            Assert.IsFalse(filter.Allows(Entry(3, DateTime.UtcNow, section: 15, username: "bob"), "alice"));
            Assert.IsFalse(filter.Allows(ReviewChangeEntry.Deleted(9, DateTime.UtcNow), "alice"));
        }

        [TestMethod]
        public void Filter_ApplyPersist_WritesSettings_AndLoadFromSettingsReadsThem()
        {
            var previousHideOwn = WebAnnotation.Properties.Settings.Default.ReviewFeedHideOwnChanges;
            var previousHideDeleted = WebAnnotation.Properties.Settings.Default.ReviewFeedHideDeleted;
            var previousUsers = WebAnnotation.Properties.Settings.Default.ReviewFeedWatchedUsers;
            var previousStructure = WebAnnotation.Properties.Settings.Default.ReviewFeedStructureOrLabel;
            var previousSections = WebAnnotation.Properties.Settings.Default.ReviewFeedSections;

            try
            {
                var filter = new ReviewChangeFeedFilter();
                filter.Apply(
                    hideOwnChanges: true,
                    watchedUsersText: "bob",
                    structureOrLabelText: "c1",
                    sectionsText: "3-5",
                    persist: true,
                    hideDeleted: false);

                Assert.IsTrue(WebAnnotation.Properties.Settings.Default.ReviewFeedHideOwnChanges);
                Assert.IsFalse(WebAnnotation.Properties.Settings.Default.ReviewFeedHideDeleted);
                Assert.AreEqual("bob", WebAnnotation.Properties.Settings.Default.ReviewFeedWatchedUsers);
                Assert.AreEqual("c1", WebAnnotation.Properties.Settings.Default.ReviewFeedStructureOrLabel);
                Assert.AreEqual("3-5", WebAnnotation.Properties.Settings.Default.ReviewFeedSections);

                ReviewChangeFeedFilter loaded = ReviewChangeFeedFilter.LoadFromSettings();
                Assert.IsTrue(loaded.HideOwnChanges);
                Assert.IsFalse(loaded.HideDeleted);
                Assert.AreEqual("bob", loaded.WatchedUsersText);
                Assert.AreEqual("c1", loaded.StructureOrLabelText);
                Assert.AreEqual("3-5", loaded.SectionsText);
                Assert.IsFalse(loaded.IsAllSections);
            }
            finally
            {
                WebAnnotation.Properties.Settings.Default.ReviewFeedHideOwnChanges = previousHideOwn;
                WebAnnotation.Properties.Settings.Default.ReviewFeedHideDeleted = previousHideDeleted;
                WebAnnotation.Properties.Settings.Default.ReviewFeedWatchedUsers = previousUsers ?? "";
                WebAnnotation.Properties.Settings.Default.ReviewFeedStructureOrLabel = previousStructure ?? "";
                WebAnnotation.Properties.Settings.Default.ReviewFeedSections = previousSections ?? "";
                WebAnnotation.Properties.Settings.Default.Save();
            }
        }
    }
}
