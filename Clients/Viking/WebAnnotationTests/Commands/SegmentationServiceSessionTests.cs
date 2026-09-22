using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;
using Viking.ViewModels;
using Viking.VolumeModel;
using WebAnnotation;
using AnnotationGlobal = WebAnnotation.Global;

namespace WebAnnotationTests
{
    /// <summary>
    /// Mid-session Select/None must update runtime URL state and clear the availability cache.
    /// </summary>
    [TestClass]
    public class SegmentationServiceSessionTests
    {
        private string? _previousSegmentationUrl;

        [TestInitialize]
        public void ResetSessionHooks()
        {
            EnsureVolumeCachePathAvailable();
            _previousSegmentationUrl = AnnotationGlobal.AnnotationSettings.SegmentationServiceUrl;

            AnnotationGlobal.InvalidateSegmentationServiceAvailability();
            State.PersistSegmentationServiceSelection = null;
            State.RecentSegmentationServiceUrls = [];
        }

        [TestCleanup]
        public void RestoreSegmentationUrl()
        {
            AnnotationGlobal.AnnotationSettings.SegmentationServiceUrl = _previousSegmentationUrl ?? string.Empty;
            State.PersistSegmentationServiceSelection = null;
            State.RecentSegmentationServiceUrls = [];
            AnnotationGlobal.InvalidateSegmentationServiceAvailability();
        }

        /// <summary>
        /// Global's type initializer reads <see cref="State.VolumeCachePath"/>, which requires a volume name.
        /// </summary>
        private static void EnsureVolumeCachePathAvailable()
        {
            if (State.volume != null)
                return;

            Volume volume = (Volume)FormatterServices.GetUninitializedObject(typeof(Volume));
            volume.Name = "WebAnnotationSessionTest";

            VolumeViewModel viewModel =
                (VolumeViewModel)FormatterServices.GetUninitializedObject(typeof(VolumeViewModel));
            FieldInfo? volumeField = typeof(VolumeViewModel).GetField(
                "_Volume",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(volumeField, "VolumeViewModel._Volume field must exist for test setup.");
            volumeField.SetValue(viewModel, volume);

            State.volume = viewModel;
        }

        [TestMethod]
        public void HasValidSegmentationServiceUrl_AcceptsHttpAndHostPort()
        {
            Assert.IsTrue(AnnotationGlobal.HasValidSegmentationServiceUrl("http://segmentation.example:50051/"));
            Assert.IsTrue(AnnotationGlobal.HasValidSegmentationServiceUrl("segmentation.example:50051"));
            Assert.IsFalse(AnnotationGlobal.HasValidSegmentationServiceUrl(null));
            Assert.IsFalse(AnnotationGlobal.HasValidSegmentationServiceUrl(""));
            Assert.IsFalse(AnnotationGlobal.HasValidSegmentationServiceUrl("   "));
            Assert.IsFalse(AnnotationGlobal.HasValidSegmentationServiceUrl("ftp://segmentation.example/"));
        }

        [TestMethod]
        public void ApplyEndpoint_SetsAnnotationSettingsAndPersists()
        {
            string? persisted = "unset";
            State.PersistSegmentationServiceSelection = e => persisted = e;

            SegmentationServiceSession.ApplyEndpoint("http://segmentation.example:50051/");

            Assert.AreEqual("http://segmentation.example:50051/", AnnotationGlobal.AnnotationSettings.SegmentationServiceUrl);
            Assert.AreEqual("http://segmentation.example:50051/", persisted);
            Assert.AreEqual(1, State.RecentSegmentationServiceUrls.Count);
            Assert.AreEqual("http://segmentation.example:50051/", State.RecentSegmentationServiceUrls[0]);
            Assert.AreEqual("http://segmentation.example:50051/", SegmentationServiceSession.CurrentEndpoint());
        }

        [TestMethod]
        public void ApplyEndpoint_NoneClearsUrlAndPersistsNull()
        {
            State.PersistSegmentationServiceSelection = e => { };
            SegmentationServiceSession.ApplyEndpoint("http://segmentation.example:50051/");

            string? persisted = "unset";
            State.PersistSegmentationServiceSelection = e => persisted = e;

            SegmentationServiceSession.ApplyEndpoint(null);

            Assert.AreEqual(string.Empty, AnnotationGlobal.AnnotationSettings.SegmentationServiceUrl);
            Assert.IsNull(persisted);
            Assert.AreEqual(string.Empty, SegmentationServiceSession.CurrentEndpoint());
            Assert.IsFalse(AnnotationGlobal.HasValidSegmentationServiceUrl(SegmentationServiceSession.CurrentEndpoint()));
        }

        [TestMethod]
        public void ApplyEndpoint_DifferentUrlRequestsAutoPolygonizeResubmit()
        {
            SegmentationServiceSession.ApplyEndpoint("http://a.example:50051/");

            int calls = 0;
            bool? lastUsable = null;
            void OnResubmit(bool usable)
            {
                calls++;
                lastUsable = usable;
            }

            SegmentationServiceSession.AutoPolygonizeResubmit += OnResubmit;
            try
            {
                SegmentationServiceSession.ApplyEndpoint("http://a.example:50051/");
                Assert.AreEqual(0, calls);

                SegmentationServiceSession.ApplyEndpoint("http://b.example:50051/");
                Assert.AreEqual(1, calls);
                Assert.AreEqual(true, lastUsable);

                SegmentationServiceSession.ApplyEndpoint(null);
                Assert.AreEqual(2, calls);
                Assert.AreEqual(false, lastUsable);
            }
            finally
            {
                SegmentationServiceSession.AutoPolygonizeResubmit -= OnResubmit;
            }
        }

        [TestMethod]
        public void InvalidateSegmentationServiceAvailability_ClearsStickyCache()
        {
            AnnotationGlobal.InvalidateSegmentationServiceAvailability();
            // Cache starts empty; calling invalidate again must remain safe for the apply path.
            AnnotationGlobal.InvalidateSegmentationServiceAvailability();
            Assert.IsFalse(AnnotationGlobal.HasValidSegmentationServiceUrl(""));
        }
    }
}
