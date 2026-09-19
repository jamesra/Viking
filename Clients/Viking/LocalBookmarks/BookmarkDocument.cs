using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using System;
using System.IO;

namespace LocalBookmarks
{
    /// <summary>
    /// One loaded bookmark XML file. Local is the volume-cache Bookmarks.xml and cannot be unloaded.
    /// Extra documents save back to <see cref="FilePath"/>; removing them from the session does not delete the file.
    /// </summary>
    internal sealed class BookmarkDocument
    {
        public BookmarkDocument(string filePath, bool isLocal, XRoot xml)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Bookmark documents need a file path.", nameof(filePath));
            if (xml is null)
                throw new ArgumentNullException(nameof(xml));

            FilePath = Path.GetFullPath(filePath);
            IsLocal = isLocal;
            Xml = xml;
        }

        public string FilePath { get; }

        public bool IsLocal { get; }

        public XRoot Xml { get; set; }

        /// <summary>
        /// Visible tree root for this file. Null until UI is attached (XML-only tests leave it unset).
        /// </summary>
        public FolderUIObj? Root { get; set; }

        public Folder Folder => Xml.Folder;

        public static bool SamePath(string left, string right) =>
            string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }
}
