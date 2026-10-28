using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>
    /// A volume's damaged sections: the <c>NonStosSectionNumbers</c> of its blocks, the sections the slice-to-volume
    /// registration was built without (torn, folded or otherwise damaged).
    /// <para>
    /// They are read from the nornir volume files next to the VikingXML. <c>&lt;VikingXML folder&gt;/VolumeData.xml</c>
    /// names each block in a <c>Block_Link Path</c>. <c>&lt;folder&gt;/&lt;block&gt;/VolumeData.xml</c> holds the
    /// comma-separated list, which is not always sorted. RC1 has none.
    /// </para>
    /// </summary>
    public static class DamagedSectionList
    {
        /// <summary>Returns a file's text, or null when it does not exist.</summary>
        public delegate Task<string> FetchText(string url, CancellationToken cancellationToken);

        /// <summary>
        /// The damaged sections of the volume whose VikingXML is <paramref name="volumeUrl"/>: empty when its blocks list
        /// none, null when the volume files could not be read (the caller then cannot tell "none" from "unknown").
        /// </summary>
        /// <param name="volumeName">
        /// The VikingXML's volume name. When the VikingXML folder has no VolumeData.xml, the volume's files are looked for
        /// in <c>&lt;parent of that folder&gt;/&lt;volumeName&gt;/</c>: RC1's VikingXML is under <c>RABBIT/</c> and its
        /// nornir files under <c>RC1/</c>.
        /// </param>
        public static async Task<SortedSet<int>> LoadAsync(string volumeUrl, Action<string> log, CancellationToken cancellationToken,
            FetchText fetch = null, string volumeName = null)
        {
            fetch ??= FetchAsync;
            string folder = FolderOf(volumeUrl);
            if (folder is null)
                return null;

            List<string> candidates = [folder];
            if (!string.IsNullOrWhiteSpace(volumeName) && FolderOf(folder) is string parent)
            {
                string named = Combine(parent, volumeName.Trim());
                if (!string.Equals(named, folder, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(named);
            }

            string root = null;
            foreach (string candidate in candidates)
            {
                try
                {
                    root = await fetch(Combine(candidate, "VolumeData.xml"), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    log?.Invoke($"Damaged sections: could not read {Combine(candidate, "VolumeData.xml")} ({ex.Message}); building without them.");
                    return null;
                }

                if (root is not null)
                {
                    folder = candidate;
                    break;
                }
            }

            if (root is null)
            {
                log?.Invoke($"Damaged sections: no VolumeData.xml in {string.Join(" or ", candidates)}; building without them.");
                return null;
            }

            IReadOnlyList<string> blocks;
            try
            {
                blocks = BlockPaths(root);
            }
            catch (System.Xml.XmlException ex)
            {
                log?.Invoke($"Damaged sections: {Combine(folder, "VolumeData.xml")} is not valid XML ({ex.Message}); building without them.");
                return null;
            }

            SortedSet<int> damaged = [];
            foreach (string block in blocks)
            {
                string url = Combine(Combine(folder, block), "VolumeData.xml");
                string text;
                try
                {
                    text = await fetch(url, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    log?.Invoke($"Damaged sections: could not read {url} ({ex.Message}); building without them.");
                    return null;
                }

                if (text is null)
                {
                    log?.Invoke($"Damaged sections: no {url}; building without them.");
                    return null;
                }

                try
                {
                    damaged.UnionWith(NonStosSections(text));
                }
                catch (System.Xml.XmlException ex)
                {
                    log?.Invoke($"Damaged sections: {url} is not valid XML ({ex.Message}); building without them.");
                    return null;
                }
            }

            log?.Invoke(damaged.Count == 0
                ? $"Damaged sections: none listed under {folder}."
                : $"Damaged sections: {damaged.Count} ({string.Join(",", damaged)}).");
            return damaged;
        }

        /// <summary>The <c>Block_Link Path</c> values of a volume-root VolumeData.xml.</summary>
        public static IReadOnlyList<string> BlockPaths(string volumeDataXml) =>
            [.. XDocument.Parse(volumeDataXml).Descendants("Block_Link")
                .Select(e => (string)e.Attribute("Path"))
                .Where(p => !string.IsNullOrWhiteSpace(p))];

        /// <summary>The integers in a block VolumeData.xml's <c>NonStosSectionNumbers</c> elements.</summary>
        public static IEnumerable<int> NonStosSections(string blockVolumeDataXml) =>
            XDocument.Parse(blockVolumeDataXml).Descendants("NonStosSectionNumbers")
                .SelectMany(e => e.Value.Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                .Select(s => int.TryParse(s, out int z) ? (int?)z : null)
                .Where(z => z.HasValue)
                .Select(z => z.Value);

        /// <summary>The folder holding the VikingXML, as a URL or a local path; null when it has none.</summary>
        internal static string FolderOf(string volumeUrl)
        {
            if (string.IsNullOrWhiteSpace(volumeUrl))
                return null;
            string trimmed = volumeUrl.Trim();
            int slash = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
            return slash <= 0 ? null : trimmed[..slash];
        }

        static string Combine(string folder, string name) =>
            IsHttp(folder) ? folder.TrimEnd('/') + "/" + name.Trim('/') : Path.Combine(folder, name);

        static bool IsHttp(string path) =>
            path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        static async Task<string> FetchAsync(string url, CancellationToken cancellationToken)
        {
            if (!IsHttp(url))
                return File.Exists(url) ? await File.ReadAllTextAsync(url, cancellationToken).ConfigureAwait(false) : null;

            using HttpResponseMessage response = await Viking.Common.SharedResources.HttpClient
                .GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
