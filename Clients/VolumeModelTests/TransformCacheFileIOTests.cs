using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// The transform cache writes through a ".part" file and swaps it in, serializes access to one path (ignoring case),
    /// deletes a cache that is not JSON (legacy BinaryFormatter data), retries reads that fail with an
    /// <see cref="IOException"/>, and waits while a ".part" file shows a save in progress.
    /// </summary>
    /// <remarks>
    /// The class is internal to VolumeModel, whose files the improvement loop may not edit, so the tests call it
    /// through reflection. Every test works in its own temporary directory.
    /// </remarks>
    [TestClass]
    public class TransformCacheFileIOTests
    {
        private static readonly Type IO =
            typeof(FixedTileCountMapping).Assembly.GetType("Viking.VolumeModel.TransformCacheFileIO", throwOnError: true);

        private string dir;

        [TestInitialize]
        public void Init()
        {
            dir = Path.Combine(Path.GetTempPath(), "TransformCacheFileIOTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        private static object Call(string method, Type generic, params object[] args)
        {
            MethodInfo m = IO.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(IO.FullName, method);
            if (generic != null)
                m = m.MakeGenericMethod(generic);
            try
            {
                return m.Invoke(null, args);
            }
            catch (TargetInvocationException e)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        private static void Save(string path, Action<Stream> writeBody) => Call("Save", null, path, writeBody);

        private static void TryDelete(string path) => Call("TryDelete", null, path);

        private static string TryLoad(string path, Func<Stream, string> readBody, out Exception lastError)
        {
            object[] args = [path, readBody, null];
            string result = (string)Call("TryLoad", typeof(string), args);
            lastError = (Exception)args[2];
            return result;
        }

        private static Action<Stream> Writes(string text) => s =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            s.Write(bytes, 0, bytes.Length);
        };

        private static string ReadAll(Stream s)
        {
            using var reader = new StreamReader(s);
            return reader.ReadToEnd();
        }

        [TestMethod]
        public void Save_CreatesMissingDirectory_WritesBody_LeavesNoPartFile()
        {
            string path = Path.Combine(dir, "sub", "deeper", "cache.json");

            Save(path, Writes("[1]"));

            Assert.AreEqual("[1]", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".part"));
        }

        [TestMethod]
        public void Save_OverExistingFile_ReplacesContent()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[\"old and longer\"]");

            Save(path, Writes("{}"));

            Assert.AreEqual("{}", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(path + ".part"));
        }

        [TestMethod]
        public void Save_WritesToPartFileFirst_TargetUntouchedUntilBodyReturns()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[\"old\"]");
            bool partSeen = false;
            string targetDuringWrite = null;

            Save(path, s =>
            {
                partSeen = File.Exists(path + ".part");
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    targetDuringWrite = ReadAll(fs);
                Writes("[\"new\"]")(s);
            });

            Assert.IsTrue(partSeen);
            Assert.AreEqual("[\"old\"]", targetDuringWrite);
            Assert.AreEqual("[\"new\"]", File.ReadAllText(path));
        }

        /// <summary>
        /// A failed body leaves the old cache in place. Current behavior also leaves the ".part" file behind.
        /// </summary>
        [TestMethod]
        public void Save_BodyThrows_Propagates_TargetUnchanged_PartFileLeft()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[\"old\"]");

            Assert.ThrowsException<InvalidOperationException>(() =>
                Save(path, s =>
                {
                    Writes("[\"half")(s);
                    throw new InvalidOperationException("boom");
                }));

            Assert.AreEqual("[\"old\"]", File.ReadAllText(path));
            Assert.IsTrue(File.Exists(path + ".part"));
        }

        /// <summary>
        /// When the swap fails (here the target is held open without sharing) the ".part" file is removed and the
        /// <see cref="IOException"/> propagates, leaving the old cache.
        /// </summary>
        [TestMethod]
        public void Save_ReplaceFails_DeletesPartFile_Rethrows()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[\"old\"]");

            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.ThrowsException<IOException>(() => Save(path, Writes("[\"new\"]")), "swap over a locked file");
                Assert.IsFalse(File.Exists(path + ".part"));
            }

            Assert.AreEqual("[\"old\"]", File.ReadAllText(path));
        }

        [TestMethod]
        public void Save_NullOrEmptyPath_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => Save(null, Writes("[]")));
            Assert.ThrowsException<ArgumentNullException>(() => Save("", Writes("[]")));
        }

        [TestMethod]
        public void TryLoad_MissingOrEmptyPath_ReturnsDefault_NoError_DoesNotCallBody()
        {
            int calls = 0;
            Func<Stream, string> body = s => { calls++; return "x"; };

            Assert.IsNull(TryLoad(Path.Combine(dir, "missing.json"), body, out Exception e1));
            Assert.IsNull(e1);
            Assert.IsNull(TryLoad(null, body, out Exception e2));
            Assert.IsNull(e2);
            Assert.IsNull(TryLoad("", body, out Exception e3));
            Assert.IsNull(e3);
            Assert.AreEqual(0, calls);
        }

        [DataTestMethod]
        [DataRow("[1,2]")]
        [DataRow("{\"a\":1}")]
        [DataRow(" \r\n\t [1]")]
        public void TryLoad_JsonFile_ReturnsBody_NoError(string content)
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, content);

            Assert.AreEqual(content, TryLoad(path, ReadAll, out Exception error));
            Assert.IsNull(error);
            Assert.IsTrue(File.Exists(path));
        }

        /// <summary>
        /// A BinaryFormatter stream starts with 0x00; an empty or whitespace-only file has no JSON start either.
        /// All are reported as <see cref="InvalidDataException"/> and deleted without calling the reader.
        /// </summary>
        [DataTestMethod]
        [DataRow(new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF })]
        [DataRow(new byte[] { (byte)'n', (byte)'u', (byte)'l', (byte)'l' })]
        [DataRow(new byte[] { (byte)' ', (byte)'\n' })]
        [DataRow(new byte[0])]
        public void TryLoad_NotJson_DeletesFile_ReportsInvalidData(byte[] content)
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllBytes(path, content);
            int calls = 0;

            string result = TryLoad(path, s => { calls++; return "x"; }, out Exception error);

            Assert.IsNull(result);
            Assert.IsInstanceOfType(error, typeof(InvalidDataException));
            StringAssert.Contains(error.Message, "not JSON");
            Assert.IsFalse(File.Exists(path));
            Assert.AreEqual(0, calls);
        }

        [TestMethod]
        public void TryLoad_BodyThrowsNonIo_NoRetry_ReturnsDefault_KeepsFile()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[]");
            int calls = 0;
            var thrown = new FormatException("bad");

            string result = TryLoad(path, s => { calls++; throw thrown; }, out Exception error);

            Assert.IsNull(result);
            Assert.AreSame(thrown, error);
            Assert.AreEqual(1, calls);
            Assert.IsTrue(File.Exists(path));
        }

        /// <summary>
        /// A transient <see cref="IOException"/> is retried. Current behavior keeps it in lastError even when the
        /// retry succeeds.
        /// </summary>
        [TestMethod]
        public void TryLoad_TransientIoException_Retries_ReturnsResult()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[]");
            int calls = 0;
            var transient = new IOException("busy");

            string result = TryLoad(path, s =>
            {
                calls++;
                if (calls == 1)
                    throw transient;
                return ReadAll(s);
            }, out Exception error);

            Assert.AreEqual("[]", result);
            Assert.AreEqual(2, calls);
            Assert.AreSame(transient, error);
        }

        /// <summary>
        /// Four attempts with 25, 50 and 75 ms back-off between them; the last error is reported.
        /// </summary>
        [TestMethod]
        public void TryLoad_PersistentIoException_TriesFourTimes_ReturnsDefault()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[]");
            int calls = 0;

            var watch = Stopwatch.StartNew();
            string result = TryLoad(path, s => { calls++; throw new IOException("busy " + calls); }, out Exception error);
            watch.Stop();

            Assert.IsTrue(watch.ElapsedMilliseconds >= 120, $"backed off only {watch.ElapsedMilliseconds} ms");
            Assert.IsNull(result);
            Assert.AreEqual(4, calls);
            Assert.IsInstanceOfType(error, typeof(IOException));
            Assert.AreEqual("busy 4", error.Message);
            Assert.IsTrue(File.Exists(path));
        }

        /// <summary>
        /// While a ".part" file exists, the reader waits 25, 50 and 75 ms, then reads the file on the last attempt.
        /// </summary>
        [TestMethod]
        public void TryLoad_PartFileExists_WaitsThenReadsOnLastAttempt()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[7]");
            File.WriteAllText(path + ".part", "[");
            int calls = 0;

            var watch = Stopwatch.StartNew();
            string result = TryLoad(path, s => { calls++; return ReadAll(s); }, out Exception error);
            watch.Stop();

            Assert.AreEqual("[7]", result);
            Assert.IsNull(error);
            Assert.AreEqual(1, calls);
            Assert.IsTrue(watch.ElapsedMilliseconds >= 120, $"waited only {watch.ElapsedMilliseconds} ms");
        }

        [TestMethod]
        public void TryLoad_PartFileRemovedDuringWait_ReadsWithoutFurtherWait()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[7]");
            File.WriteAllText(path + ".part", "[");
            int calls = 0;
            bool partExistedAtRead = true;

            var remover = Task.Run(() =>
            {
                Thread.Sleep(10);
                File.Delete(path + ".part");
            });

            string result = TryLoad(path, s =>
            {
                calls++;
                partExistedAtRead = File.Exists(path + ".part");
                return ReadAll(s);
            }, out Exception error);
            remover.Wait();

            Assert.AreEqual("[7]", result);
            Assert.IsNull(error);
            Assert.AreEqual(1, calls);
            Assert.IsFalse(partExistedAtRead);
        }

        [TestMethod]
        public void TryDelete_RemovesFile_IgnoresMissingAndEmptyPaths()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[]");

            TryDelete(path);
            Assert.IsFalse(File.Exists(path));

            TryDelete(path);
            TryDelete(null);
            TryDelete("");

            // The per-path lock was released: a later save of the same path does not block.
            var save = Task.Run(() => Save(path, Writes("[]")));
            Assert.IsTrue(save.Wait(TimeSpan.FromSeconds(10)), "save blocked after TryDelete");
            Assert.AreEqual("[]", File.ReadAllText(path));
        }

        /// <summary>
        /// A delete of the same path, spelled with different case, waits until an in-progress save finishes.
        /// </summary>
        [TestMethod]
        public void TryDelete_WaitsForSaveOnSamePathIgnoringCase()
        {
            string path = Path.Combine(dir, "cache.json");
            File.WriteAllText(path, "[\"old\"]");
            using var inBody = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();

            var save = Task.Run(() => Save(path, s =>
            {
                inBody.Set();
                release.Wait(TimeSpan.FromSeconds(10));
                Writes("[\"new\"]")(s);
            }));
            Assert.IsTrue(inBody.Wait(TimeSpan.FromSeconds(10)));

            var delete = Task.Run(() => TryDelete(path.ToUpperInvariant()));
            bool deleteFinishedWhileSaving = delete.Wait(150);
            release.Set();

            Assert.IsTrue(save.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(delete.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsFalse(deleteFinishedWhileSaving);
            Assert.IsFalse(File.Exists(path));
        }
    }
}
