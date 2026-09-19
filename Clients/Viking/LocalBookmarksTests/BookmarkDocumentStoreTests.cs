using System;
using System.IO;
using System.Linq;
using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using LocalBookmarks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LocalBookmarksTests
{
    [TestClass]
    public class BookmarkDocumentStoreTests
    {
        [TestMethod]
        public void Load_two_documents_does_not_replace_local()
        {
            using TempBookmarkSession session = new();
            BookmarkDocumentStore store = session.Store;

            Assert.AreEqual("Local", store.DisplayNameOf(store.Local));
            Assert.AreEqual(1, store.Documents.Count);

            XRoot extraXml = BookmarkXml.Create("extra-root");
            extraXml.Save(session.ExtraPath);
            Assert.IsTrue(store.TryAddExtra(session.ExtraPath, XRoot.Load(session.ExtraPath), out BookmarkDocument extra));

            Assert.AreEqual(2, store.Documents.Count);
            Assert.AreSame(store.Local, store.Documents[0]);
            Assert.AreEqual(session.ExtraPath, extra.FilePath);
            Assert.AreEqual("colleague.xml", store.DisplayNameOf(extra));
            Assert.AreEqual("extra-root", extra.Folder.Name);
            Assert.AreEqual("local-root", store.Local.Folder.Name);
        }

        [TestMethod]
        public void Unload_extra_keeps_the_file_and_cannot_remove_local()
        {
            using TempBookmarkSession session = new();
            BookmarkDocumentStore store = session.Store;
            store.TryAddExtra(session.ExtraPath, XRoot.Load(session.ExtraPath), out BookmarkDocument extra);

            Assert.IsFalse(store.TryUnload(store.Local));
            Assert.AreEqual(2, store.Documents.Count);

            Assert.IsTrue(store.TryUnload(extra));
            Assert.AreEqual(1, store.Documents.Count);
            Assert.IsTrue(File.Exists(session.ExtraPath));
        }

        [TestMethod]
        public void Save_extra_does_not_rewrite_local()
        {
            using TempBookmarkSession session = new();
            BookmarkDocumentStore store = session.Store;
            store.TryAddExtra(session.ExtraPath, XRoot.Load(session.ExtraPath), out BookmarkDocument extra);

            string localBefore = File.ReadAllText(session.LocalPath);
            extra.Folder.Name = "mutated-extra";
            store.WriteXml(extra);

            Assert.AreEqual(localBefore, File.ReadAllText(session.LocalPath));
            Assert.AreEqual("mutated-extra", XRoot.Load(session.ExtraPath).Folder.Name);
        }

        [TestMethod]
        public void Duplicate_filenames_get_parent_folder_in_display_name()
        {
            using TempBookmarkSession session = new();
            string otherDir = Path.Combine(session.Directory, "other");
            Directory.CreateDirectory(otherDir);
            string otherPath = Path.Combine(otherDir, "colleague.xml");
            BookmarkXml.Create("other").Save(otherPath);

            BookmarkDocumentStore store = session.Store;
            store.TryAddExtra(session.ExtraPath, XRoot.Load(session.ExtraPath), out BookmarkDocument first);
            store.TryAddExtra(otherPath, XRoot.Load(otherPath), out BookmarkDocument second);

            StringAssert.Contains(store.DisplayNameOf(first), "colleague.xml");
            StringAssert.Contains(store.DisplayNameOf(first), "(");
            StringAssert.Contains(store.DisplayNameOf(second), "other");
        }

        [TestMethod]
        public void Sidecar_round_trips_extra_paths_without_local()
        {
            using TempBookmarkSession session = new();
            BookmarkDocumentStore store = session.Store;
            store.TryAddExtra(session.ExtraPath, XRoot.Load(session.ExtraPath), out _);
            store.PersistSidecar();

            string sidecar = BookmarkDocumentStore.SidecarPathFor(session.LocalPath);
            string[] lines = File.ReadAllLines(sidecar);
            CollectionAssert.AreEqual(new[] { session.ExtraPath }, lines);
            CollectionAssert.AreEqual(new[] { session.ExtraPath }, store.ReadSidecarPaths().ToArray());
        }

        [TestMethod]
        public void Adding_local_path_again_is_ignored()
        {
            using TempBookmarkSession session = new();
            Assert.IsFalse(session.Store.TryAddExtra(session.LocalPath, XRoot.Load(session.LocalPath), out BookmarkDocument document));
            Assert.AreSame(session.Store.Local, document);
            Assert.AreEqual(1, session.Store.Documents.Count);
        }
    }

    internal static class BookmarkXml
    {
        public static XRoot Create(string folderName)
        {
            Folder folder = new()
            {
                Name = folderName
            };
            return new XRoot(folder);
        }
    }

    internal sealed class TempBookmarkSession : IDisposable
    {
        public TempBookmarkSession()
        {
            Directory = Path.Combine(Path.GetTempPath(), "VikingBookmarks-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            LocalPath = Path.GetFullPath(Path.Combine(Directory, "Bookmarks.xml"));
            ExtraPath = Path.GetFullPath(Path.Combine(Directory, "colleague.xml"));
            BookmarkXml.Create("local-root").Save(LocalPath);
            BookmarkXml.Create("extra-root").Save(ExtraPath);
            Store = BookmarkDocumentStore.FromLocal(LocalPath, XRoot.Load(LocalPath));
        }

        public string Directory { get; }
        public string LocalPath { get; }
        public string ExtraPath { get; }
        public BookmarkDocumentStore Store { get; }

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                    System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
