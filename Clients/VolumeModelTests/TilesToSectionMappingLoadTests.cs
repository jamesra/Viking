using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;
using Path = System.IO.Path;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins how <see cref="TilesToSectionMapping.LoadTransform"/> uses its cache: one GET of the .mosaic with
    /// <c>If-Modified-Since</c> set to the server date stored in the cached transforms; a 304 uses the cache, a 200 parses
    /// that same response and replaces the cache, and an unreachable server falls back to the cache.
    /// </summary>
    /// <remarks>
    /// The section and volume are uninitialized objects with only the fields the mapping reads (section number, cache
    /// folder). <see cref="Viking.Common.SharedResources.HttpClient"/> is replaced per test.
    /// </remarks>
    [TestClass]
    public class TilesToSectionMappingLoadTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);
        private static readonly DateTimeOffset BuiltDate = new(2026, 9, 9, 1, 36, 11, TimeSpan.Zero);
        private static readonly DateTimeOffset RebuiltDate = new(2026, 11, 5, 19, 6, 38, TimeSpan.Zero);

        private const string OneTile =
            "format_version_number: 0\nnumber_of_images: 1\npixel_spacing: 1\nuse_std_mask: 0\n" +
            "image: 001.png TranslationTransform_double_2_2 vp 2 10 20 fp 4 0 0 128 128\n";

        private const string TwoTiles =
            "format_version_number: 0\nnumber_of_images: 2\npixel_spacing: 1\nuse_std_mask: 0\n" +
            "image: 001.png TranslationTransform_double_2_2 vp 2 10 20 fp 4 0 0 128 128\n" +
            "image: 002.png TranslationTransform_double_2_2 vp 2 300 20 fp 4 0 0 128 128\n";

        private HttpClient _savedClient;
        private string _root = "";

        /// <summary>Plays nginx: 304 when <c>If-Modified-Since</c> equals <see cref="LastModified"/> exactly, else 200.</summary>
        private sealed class MosaicServer : HttpMessageHandler
        {
            public string Mosaic { get; set; } = OneTile;
            public DateTimeOffset? LastModified { get; set; } = BuiltDate;
            public bool Offline { get; set; }
            public List<DateTimeOffset?> IfModifiedSince { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                IfModifiedSince.Add(request.Headers.IfModifiedSince);
                if (Offline)
                    throw new HttpRequestException("No route to host");

                if (LastModified.HasValue && request.Headers.IfModifiedSince == LastModified)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified) { RequestMessage = request });

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new ByteArrayContent(Encoding.ASCII.GetBytes(Mosaic)),
                };
                response.Content.Headers.LastModified = LastModified;
                return Task.FromResult(response);
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            _savedClient = Viking.Common.SharedResources.HttpClient;
            _root = Path.Combine(Path.GetTempPath(), "TilesToSectionMappingLoad-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Viking.Common.SharedResources.HttpClient = _savedClient;
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        private static object Uninitialized(Type type)
        {
#if NETFRAMEWORK
            return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#else
            return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
#endif
        }

        private TilesToSectionMapping NewMapping()
        {
            var volume = (Volume)Uninitialized(typeof(Volume));
            typeof(Volume).GetField("Paths", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(volume, new VolumePaths(_root, "RC2"));
            var section = (Section)Uninitialized(typeof(Section));
            section.volume = volume;
            section.Number = 1240;
            return new TilesToSectionMapping(section, "Grid", "http://mosaic.test/RC2/TEM/1240/TEM", "Grid.mosaic", "", "");
        }

        private MosaicServer Serve(MosaicServer server)
        {
            Viking.Common.SharedResources.HttpClient = new HttpClient(server);
            return server;
        }

        private static ITransform[] Load(TilesToSectionMapping mapping) =>
            Task.Run(() => mapping.LoadTransform(CancellationToken.None)).GetAwaiter().GetResult();

        /// <summary>The first load saves the cache in the background; wait until it is in place.</summary>
        private static void WaitForCache(TilesToSectionMapping mapping, DateTimeOffset expectedServerDate)
        {
            DateTime deadline = DateTime.UtcNow + Patience;
            while (true)
            {
                string file = mapping.CachedTransformsFileName;
                if (File.Exists(file) && !File.Exists(file + ".part"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        if (json.Contains(expectedServerDate.UtcDateTime.ToBinary().ToString()))
                            return;
                    }
                    catch (IOException)
                    {
                    }
                }

                Assert.IsTrue(DateTime.UtcNow < deadline, "The transform cache was not saved.");
                Thread.Sleep(20);
            }
        }

        [TestMethod]
        public void FirstLoadDownloadsWithoutADateAndKeepsTheServersDate()
        {
            MosaicServer server = Serve(new MosaicServer());
            TilesToSectionMapping mapping = NewMapping();

            ITransform[] transforms = Load(mapping);

            Assert.AreEqual(1, transforms.Length);
            CollectionAssert.AreEqual(new DateTimeOffset?[] { null }, server.IfModifiedSince);
            Assert.AreEqual(BuiltDate.UtcDateTime, TilesToSectionMapping.CachedServerLastModified(transforms));
            Assert.AreEqual(BuiltDate.UtcDateTime, mapping.LastModified);
        }

        [TestMethod]
        public void UnchangedMosaicIsOneRequestAnswered304FromTheCache()
        {
            MosaicServer server = Serve(new MosaicServer());
            TilesToSectionMapping first = NewMapping();
            Load(first);
            WaitForCache(first, BuiltDate);
            server.IfModifiedSince.Clear();

            ITransform[] transforms = Load(NewMapping());

            Assert.AreEqual(1, transforms.Length);
            CollectionAssert.AreEqual(new DateTimeOffset?[] { BuiltDate }, server.IfModifiedSince,
                "One request, carrying the server's own date.");
        }

        [TestMethod]
        public void RebuiltMosaicReplacesTheCacheFromTheSameResponse()
        {
            MosaicServer server = Serve(new MosaicServer());
            TilesToSectionMapping first = NewMapping();
            Load(first);
            WaitForCache(first, BuiltDate);
            server.IfModifiedSince.Clear();
            server.Mosaic = TwoTiles;
            server.LastModified = RebuiltDate;

            TilesToSectionMapping second = NewMapping();
            ITransform[] transforms = Load(second);

            Assert.AreEqual(2, transforms.Length);
            Assert.AreEqual(1, server.IfModifiedSince.Count, "The 200 that reports the change is also the download.");
            Assert.AreEqual(RebuiltDate.UtcDateTime, second.LastModified, "The warped cache compares against the new date.");
            WaitForCache(second, RebuiltDate);
            server.IfModifiedSince.Clear();
            Assert.AreEqual(2, Load(NewMapping()).Length);
            CollectionAssert.AreEqual(new DateTimeOffset?[] { RebuiltDate }, server.IfModifiedSince);
        }

        [TestMethod]
        public void UnreachableServerUsesTheCache()
        {
            MosaicServer server = Serve(new MosaicServer());
            TilesToSectionMapping first = NewMapping();
            Load(first);
            WaitForCache(first, BuiltDate);
            server.Offline = true;

            Assert.AreEqual(1, Load(NewMapping()).Length);
        }

        [TestMethod]
        public void UnreachableServerWithoutACacheThrows()
        {
            Serve(new MosaicServer { Offline = true });

            Assert.ThrowsException<HttpRequestException>(() => Load(NewMapping()));
        }
    }
}
