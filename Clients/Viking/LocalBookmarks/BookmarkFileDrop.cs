using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LocalBookmarks
{
    /// <summary>
    /// Parses Explorer <see cref="DataFormats.FileDrop"/> payloads for bookmark XML files.
    /// WinForms supplies a <c>string[]</c>, not a single string — asking for <c>typeof(string)</c> always yields null.
    /// </summary>
    public static class BookmarkFileDrop
    {
        /// <summary>
        /// True when the data object contains at least one existing <c>.xml</c> path.
        /// Used by tree DragEnter/DragOver to show Copy without loading files yet.
        /// </summary>
        public static bool IsXmlFileDrop(IDataObject? data) => GetDroppedXmlPaths(data).Length > 0;

        /// <summary>
        /// Existing <c>.xml</c> paths from a file-drop. Empty when the payload is missing, the wrong type, or not XML.
        /// </summary>
        public static string[] GetDroppedXmlPaths(IDataObject? data)
        {
            if (data is null || !data.GetDataPresent(DataFormats.FileDrop))
                return [];

            if (data.GetData(DataFormats.FileDrop) is not string[] paths)
                return [];

            return [.. paths.Where(IsExistingXmlPath)];
        }

        private static bool IsExistingXmlPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase)
                && File.Exists(path);
        }
    }
}
