using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LocalBookmarks
{
    /// <summary>
    /// Session list of bookmark documents. Index 0 is always Local. Extra paths persist in
    /// <c>LoadedFiles.txt</c> next to Local so they reload on the next volume session.
    /// </summary>
    internal sealed class BookmarkDocumentStore
    {
        internal const string SidecarFileName = "LoadedFiles.txt";

        private readonly List<BookmarkDocument> _documents = [];
        private readonly string _sidecarPath;

        public BookmarkDocumentStore(BookmarkDocument local, string sidecarPath)
        {
            if (local is null)
                throw new ArgumentNullException(nameof(local));
            if (!local.IsLocal)
                throw new ArgumentException("The first document must be Local.", nameof(local));
            if (string.IsNullOrWhiteSpace(sidecarPath))
                throw new ArgumentException("Sidecar path is required.", nameof(sidecarPath));

            _sidecarPath = sidecarPath;
            _documents.Add(local);
        }

        public BookmarkDocument Local => _documents[0];

        public IReadOnlyList<BookmarkDocument> Documents => _documents;

        public static string SidecarPathFor(string localBookmarkPath)
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(localBookmarkPath));
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("Local bookmark path must include a directory.", nameof(localBookmarkPath));
            return Path.Combine(directory, SidecarFileName);
        }

        public static BookmarkDocumentStore FromLocal(string localPath, XRoot xml)
        {
            BookmarkDocument local = new(localPath, isLocal: true, xml);
            return new BookmarkDocumentStore(local, SidecarPathFor(local.FilePath));
        }

        /// <summary>
        /// Tree label: "Local" or the filename, with a parent-folder suffix when two extras share a name.
        /// </summary>
        public string DisplayNameOf(BookmarkDocument document)
        {
            if (document.IsLocal)
                return "Local";

            string fileName = Path.GetFileName(document.FilePath);
            bool collision = _documents.Any(other =>
                !other.IsLocal
                && !ReferenceEquals(other, document)
                && string.Equals(Path.GetFileName(other.FilePath), fileName, StringComparison.OrdinalIgnoreCase));

            if (!collision)
                return fileName;

            string parent = Path.GetFileName(Path.GetDirectoryName(document.FilePath) ?? string.Empty);
            return string.IsNullOrEmpty(parent) ? fileName : $"{fileName} ({parent})";
        }

        public BookmarkDocument? FindByPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string full = Path.GetFullPath(path);
            return _documents.FirstOrDefault(d => BookmarkDocument.SamePath(d.FilePath, full));
        }

        /// <summary>
        /// Walks to the file-root folder (Parent == null) and matches <see cref="BookmarkDocument.Root"/>.
        /// Falls back to Local when UI is not attached.
        /// </summary>
        public BookmarkDocument FindOwner(FolderUIObj? folder)
        {
            while (folder?.Parent != null)
                folder = folder.Parent;

            if (folder is null)
                return Local;

            return _documents.FirstOrDefault(d => ReferenceEquals(d.Root, folder)) ?? Local;
        }

        /// <summary>
        /// Adds an extra file. Returns false when the path is already Local or already loaded
        /// (<paramref name="document"/> is the existing instance).
        /// </summary>
        public bool TryAddExtra(string path, XRoot xml, out BookmarkDocument document)
        {
            if (xml is null)
                throw new ArgumentNullException(nameof(xml));

            BookmarkDocument? existing = FindByPath(path);
            if (existing != null)
            {
                document = existing;
                return false;
            }

            if (BookmarkDocument.SamePath(path, Local.FilePath))
            {
                document = Local;
                return false;
            }

            document = new BookmarkDocument(path, isLocal: false, xml);
            _documents.Add(document);
            return true;
        }

        /// <summary>
        /// Removes an extra document from the session. Always returns false for Local; never deletes the file on disk.
        /// </summary>
        public bool TryUnload(BookmarkDocument document)
        {
            if (document is null || document.IsLocal)
                return false;

            return _documents.Remove(document);
        }

        public void WriteXml(BookmarkDocument document)
        {
            if (document is null)
                throw new ArgumentNullException(nameof(document));

            document.Xml.Save(document.FilePath);
        }

        public IReadOnlyList<string> ReadSidecarPaths()
        {
            if (!File.Exists(_sidecarPath))
                return [];

            return [.. File.ReadAllLines(_sidecarPath)
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0)
                .Select(static line =>
                {
                    try
                    {
                        return Path.GetFullPath(line);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                })
                .Where(static path => path != null && File.Exists(path))
                .Cast<string>()
                .Where(path => !BookmarkDocument.SamePath(path, Local.FilePath))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        public void PersistSidecar()
        {
            string? directory = Path.GetDirectoryName(_sidecarPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string[] extras = [.. _documents.Skip(1).Select(d => d.FilePath)];
            File.WriteAllLines(_sidecarPath, extras);
        }
    }
}
