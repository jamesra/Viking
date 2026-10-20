using System.IO;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.Forms;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="ScreenshotForm.SplitPath"/>, which fills the Screenshot dialog's Folder and File boxes from the
    /// path picked with Browse (and from the Filename setter). The PNG is saved to Path.Combine(Folder, File), and the
    /// meta .txt goes next to it, so the folder must land in Folder and the name, extension included, in File.
    /// </summary>
    [TestClass]
    public class ScreenshotFormSplitPathTests
    {
        /// <summary>
        /// Regression: Browse put the folder (C:\Shots) into the File box and left Folder unchanged,
        /// so the capture was written to the path C:\Shots instead of C:\Shots\cell7.png.
        /// </summary>
        [TestMethod]
        public void BrowsedPath_PutsFolderInFolderAndNameWithExtensionInFile()
        {
            (string folder, string fileName) = ScreenshotForm.SplitPath(@"C:\Shots\cell7.png");

            Assert.AreEqual(@"C:\Shots", folder);
            Assert.AreEqual("cell7.png", fileName);
        }

        [TestMethod]
        public void FileInDriveRoot_KeepsTheRootAsFolder()
        {
            (string folder, string fileName) = ScreenshotForm.SplitPath(@"D:\cell7_003.png");

            Assert.AreEqual(@"D:\", folder);
            Assert.AreEqual("cell7_003.png", fileName);
        }

        /// <summary>
        /// For any folder and file name, splitting the combined path gives them back, so the dialog's
        /// Filename (Folder combined with File) is the path that was browsed to.
        /// </summary>
        [TestMethod]
        public void SplitThenCombine_ReturnsThePickedPath()
        {
            Gen<string> segment = Gen.Elements("Shots", "cell 7", "a.b", "x", "RC1_section-12");
            Gen<string> folder = Gen.Elements(@"C:\", @"D:\", @"\\server\share\")
                .SelectMany(root => Gen.ArrayOf(segment).Select(parts => Path.Combine(root, Path.Combine(parts))));
            Gen<string> name = segment.Select(s => s + ".png");

            Prop.ForAll(Arb.From(folder), Arb.From(name), (string dir, string file) =>
            {
                string picked = Path.Combine(dir, file);
                (string splitFolder, string splitName) = ScreenshotForm.SplitPath(picked);
                return splitName == file && Path.Combine(splitFolder, splitName) == picked;
            }).QuickCheckThrowOnFailure();
        }
    }
}
