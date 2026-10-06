using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
#if VIKINGBENCH_WCF_ANNOTATIONS
using WebAnnotationModel;
#endif

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>
    /// The one-time <c>prime</c> step: mirrors the files the benchmark sections need and saves a read-only annotation
    /// snapshot. Only this step talks to the volume server and the annotation service.
    /// </summary>
    /// <remarks>
    /// The annotation fetch only reads (<c>LocationStore.GetObjectsForSection</c>). Nothing here calls Save or any
    /// other write. Credentials come from VIKINGBENCH_USER and VIKINGBENCH_PASSWORD and are never written anywhere.
    /// </remarks>
    internal static class Primer
    {
        public static async Task<int> RunAsync(BenchOptions options)
        {
            using HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };

            string volumeDir = Path.Combine(options.MirrorRoot, options.VolumeServerPath.Replace('/', Path.DirectorySeparatorChar));
            string xmlPath = Path.Combine(volumeDir, options.VolumeFileName);
            await DownloadAsync(http, options.VolumeUrl, xmlPath, force: true).ConfigureAwait(false);

            XElement volume = XDocument.Load(xmlPath).Root;
            string host = options.VolumeHost;

            foreach (XElement group in volume.Elements().Where(e => e.Name.LocalName.Equals("StosGroup", StringComparison.OrdinalIgnoreCase)))
            {
                string zip = Attr(group, "zip");
                if (zip != null)
                    await DownloadAsync(http, $"{host}/{zip}", Path.Combine(volumeDir, zip)).ConfigureAwait(false);
            }

            string volumeZip = Attr(volume, "StosZip");
            if (volumeZip != null)
                await DownloadAsync(http, $"{host}/{volumeZip}", Path.Combine(volumeDir, volumeZip)).ConfigureAwait(false);

            XElement sections = volume.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Sections", StringComparison.OrdinalIgnoreCase)) ?? volume;
            Dictionary<int, XElement> sectionElements = sections.Elements()
                .Where(e => e.Name.LocalName.Equals("Section", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(e => int.Parse(Attr(e, "Number"), CultureInfo.InvariantCulture));

            foreach (int number in options.Sections)
            {
                if (!sectionElements.TryGetValue(number, out XElement section))
                {
                    Console.WriteLine($"Section {number} is not in the volume; skipped.");
                    continue;
                }

                string sectionPath = Attr(section, "Path");
                foreach (XElement transform in section.Elements().Where(e => e.Name.LocalName.Equals("Transform", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!string.Equals(Attr(transform, "Name"), options.MosaicTransform, StringComparison.Ordinal))
                        continue;

                    string mosaic = $"{sectionPath}/{Attr(transform, "Path")}";
                    await DownloadAsync(http, $"{host}/{mosaic}", Path.Combine(volumeDir, mosaic)).ConfigureAwait(false);
                }
            }

            return SnapshotAnnotations(options, volume);
        }

#if !VIKINGBENCH_WCF_ANNOTATIONS
        /// <summary>
        /// This build has no WCF annotation store (VikingLegacy splits it into a separate project). The snapshot format is
        /// tree-independent, so copy the <c>annotations</c> folder from a VikingLegacy-Maintenance prime instead.
        /// </summary>
        private static int SnapshotAnnotations(BenchOptions options, XElement volume)
        {
            _ = volume;
            Console.WriteLine($"Annotation snapshot not supported in this build. Copy <root>\\annotations from a VikingLegacy-Maintenance prime into {options.AnnotationRoot}.");
            return 0;
        }
#else
        private static int SnapshotAnnotations(BenchOptions options, XElement volume)
        {
            XElement endpointElement = volume.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("VolumeToEndpoint", StringComparison.OrdinalIgnoreCase));
            string endpoint = endpointElement is null ? null : Attr(endpointElement, "Endpoint");
            if (endpoint is null)
            {
                Console.WriteLine("The VikingXML has no <VolumeToEndpoint>; no annotation snapshot taken. Phases C, D3 and E2 will be skipped.");
                return 0;
            }

            State.Endpoint = new Uri(endpoint);
            string user = Environment.GetEnvironmentVariable("VIKINGBENCH_USER");
            string password = Environment.GetEnvironmentVariable("VIKINGBENCH_PASSWORD");
            bool haveLogin = !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password);
            if (haveLogin)
                State.UserCredentials = new System.Net.NetworkCredential(user, password);

            Console.WriteLine($"Annotation snapshot from {endpoint} using {(haveLogin ? "VIKINGBENCH_USER" : "the anonymous login")}.");

            LocationStore store = new();
            int emptySections = 0;
            foreach (int number in options.Sections)
            {
                var locations = store.GetObjectsForSection(number);
                SectionSnapshot snapshot = new()
                {
                    Section = number,
                    FetchedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    Endpoint = endpoint,
                };

                foreach (LocationObj loc in locations.Values.OrderBy(l => l.ID))
                {
                    snapshot.Locations.Add(new SnapshotLocation
                    {
                        Id = loc.ID,
                        ParentId = loc.ParentID,
                        TypeCode = (short)loc.TypeCode,
                        Width = loc.Width,
                        LastModifiedTicks = loc.LastModified.Ticks,
                        Z = loc.Z,
                        MosaicWkb = loc.MosaicShape is null ? null : Convert.ToBase64String(loc.MosaicShape.STAsBinary().Value),
                        VolumeWkb = loc.VolumeShape is null ? null : Convert.ToBase64String(loc.VolumeShape.STAsBinary().Value),
                    });
                }

                snapshot.Save(options);
                if (snapshot.Locations.Count == 0)
                    emptySections++;
                Console.WriteLine($"  Section {number}: {snapshot.Locations.Count} locations");
                store.RemoveSection(number);
            }

            if (emptySections == options.Sections.Length)
            {
                Console.WriteLine("Every section returned 0 locations. LocationStore swallows errors, so querying directly to see why:");
                Console.WriteLine("  " + DiagnoseAnnotationQuery(endpoint, options.Sections[0]));
                Console.WriteLine("Set VIKINGBENCH_USER and VIKINGBENCH_PASSWORD and rerun prime to take the snapshot.");
                return 2;
            }

            return 0;
        }

        /// <summary>One read-only location query that reports the exception <c>LocationStore</c> would have swallowed.</summary>
        private static string DiagnoseAnnotationQuery(string endpoint, int section)
        {
            using System.ServiceModel.ChannelFactory<WebAnnotationModel.Service.IAnnotateLocations> factory =
                new("Annotation.Service.Interfaces.IAnnotateLocations-Binary");
            factory.Credentials.UserName.UserName = State.UserCredentials.UserName;
            factory.Credentials.UserName.Password = State.UserCredentials.Password;
            var channel = factory.CreateChannel(new System.ServiceModel.EndpointAddress(endpoint));
            try
            {
                var locations = channel.GetLocationChanges(out _, out _, section, 0);
                ((System.ServiceModel.IClientChannel)channel).Close();
                return $"Direct query succeeded with {locations?.Length ?? 0} locations for section {section}.";
            }
            catch (Exception e)
            {
                ((System.ServiceModel.IClientChannel)channel).Abort();
                Exception inner = e;
                while (inner.InnerException != null)
                    inner = inner.InnerException;
                return $"Direct query failed: {e.GetType().Name}: {e.Message}" + (inner == e ? "" : $" (inner {inner.GetType().Name}: {inner.Message})");
            }
        }

#endif

        private static string Attr(XElement e, string name) =>
            e.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

        /// <summary>
        /// Downloads <paramref name="url"/> to <paramref name="path"/> and sets the file's write time to the server's
        /// Last-Modified, so the mirror reports the same modification time as the original server. Skips files that already
        /// match the server's length and time unless <paramref name="force"/> is set.
        /// </summary>
        private static async Task DownloadAsync(HttpClient http, string url, string path, bool force = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"  {(int)response.StatusCode} {url}");
                return;
            }

            DateTime lastModified = response.Content.Headers.LastModified?.UtcDateTime ?? DateTime.UtcNow;
            long? length = response.Content.Headers.ContentLength;
            FileInfo existing = new(path);
            if (!force && existing.Exists && length == existing.Length && existing.LastWriteTimeUtc == lastModified)
            {
                Console.WriteLine($"  up to date {url}");
                return;
            }

            string temp = path + ".download";
            using (Stream body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (FileStream file = File.Create(temp))
                await body.CopyToAsync(file).ConfigureAwait(false);

            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
            File.SetLastWriteTimeUtc(path, lastModified);
            Console.WriteLine($"  {new FileInfo(path).Length,12:N0} bytes  {url}");
        }
    }
}
