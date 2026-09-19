using System;
using System.IO;
using System.Windows.Forms;
using LocalBookmarks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LocalBookmarksTests
{
    [TestClass]
    public class BookmarkFileDropTests
    {
        [TestMethod]
        public void GetDroppedXmlPaths_reads_string_array_not_single_string()
        {
            string xml = CreateTempXml("drop.xml");
            try
            {
                DataObject data = new();
                data.SetData(DataFormats.FileDrop, new[] { xml });

                Assert.IsTrue(BookmarkFileDrop.IsXmlFileDrop(data));
                CollectionAssert.AreEqual(new[] { xml }, BookmarkFileDrop.GetDroppedXmlPaths(data));
            }
            finally
            {
                File.Delete(xml);
            }
        }

        [TestMethod]
        public void GetDroppedXmlPaths_ignores_non_xml()
        {
            string txt = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(txt, "nope");
            try
            {
                DataObject data = new();
                data.SetData(DataFormats.FileDrop, new[] { txt });
                Assert.AreEqual(0, BookmarkFileDrop.GetDroppedXmlPaths(data).Length);
                Assert.IsFalse(BookmarkFileDrop.IsXmlFileDrop(data));
            }
            finally
            {
                File.Delete(txt);
            }
        }

        private static string CreateTempXml(string fileName)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "-" + fileName);
            File.WriteAllText(path, "<root/>");
            return path;
        }
    }
}
