using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using Viking;


namespace VikingTests
{
    /// <summary>
    /// Summary description for LocalTextureCacheTest
    /// </summary>
    [TestClass]
    public class LocalTextureCacheTest
    {
        LocalTextureCache cache = new LocalTextureCache();

        public int NumTests = 100;

        public LocalTextureCacheTest()
        {

            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        private IEnumerable<IAsyncResult> ReadEveryFile(List<string> FileList)
        {

            List<IAsyncResult> results = new List<IAsyncResult>(FileList.Count);

            for (int iFile = 0; iFile < NumTests; iFile++)
            {
                Stream localFile = this.cache.Fetch(FileList[iFile]);
                Byte[] buffer = new Byte[localFile.Length];

                IAsyncResult result = localFile.BeginRead(buffer, 0, (int)localFile.Length, OnReadComplete, localFile);
                results.Add(result);
            }

            return results;
        }

        private void OnReadComplete(IAsyncResult result)
        {
            Stream localFile = (Stream)result.AsyncState;

            if (localFile != null)
            {
                localFile.Close();
                localFile.Dispose();
            }
        }

        private void ClearDirectoryFiles(string path)
        {
            foreach (string filepath in System.IO.Directory.EnumerateFiles(path))
            {
                System.IO.File.Delete(filepath);
            }
        }

        private string SetupOutputDir()
        {
            string TestDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VikingUnitTests");

            if (!System.IO.Directory.Exists(TestDir))
            {
                System.IO.Directory.CreateDirectory(TestDir);

                this.ClearDirectoryFiles(TestDir);
            }

            return TestDir;
        }

        [TestMethod]
        public void TestLocalTextureCache()
        {
            Random r = new Random(1);

            List<String> TempFiles = new List<string>(NumTests);

            string TestDir = SetupOutputDir();

            for (int iTest = 0; iTest < NumTests; iTest++)
            {
                string TempFile = System.IO.Path.Combine(TestDir, iTest.ToString() + ".dat");
                TempFiles.Add(TempFile);
            }

            for (int iTest = 0; iTest < NumTests; iTest++)
            {
                int NumBytes = (NumTests - iTest) * 100;
                Byte[] bytes = new Byte[NumBytes];

                r.NextBytes(bytes);

                this.cache.AddAsync(TempFiles[iTest], bytes);
            }

            //Touch every file so it has a checkpoint flag
            IEnumerable<IAsyncResult> results = ReadEveryFile(TempFiles);


            //Run a checkpoint, nothing should be deleted since every file was accessed
            cache.Checkpoint();

            //All of the files were used, so they should still be present
            for (int iTest = 0; iTest < NumTests; iTest++)
            {
                Assert.IsTrue(System.IO.File.Exists(TempFiles[iTest]), "File was used before last checkpoint so it should exist on disk");
            }

            //Touch every file again, run two checkpoints so the files are deleted.  Make sure the async operations do not crash
            results = ReadEveryFile(TempFiles);

            cache.Checkpoint();
            cache.Checkpoint();   //Some of the reads should still be occurring at the second checkpoint and a file in use should be deleted.

            //Remove files at the end of the test
            //All of the files should be removed by the checkpoint
            //for (int iTest = 0; iTest < NumTests; iTest++)
            //{
            //Assert.IsTrue(System.IO.File.Exists(TempFiles[iTest]), "File " + TempFiles[iTest] + " was used before last checkpoint so it should exist on disk");
            //}

            //Wait for all reads to complete
            foreach (IAsyncResult result in results)
            {
                if (!result.IsCompleted)
                {
                    result.AsyncWaitHandle.WaitOne();
                }
            }

            ClearDirectoryFiles(TestDir);
        }
    }
}
