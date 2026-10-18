using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace SqlGeometryUtilsTest
{
    /// <summary>
    /// Pins <see cref="SqlServerTypesLoader.Loader.LoadNativeAssemblies"/> against the native
    /// SQL Server spatial DLL set that MonogameTestbed ships (msvcr120 plus SqlServerSpatial140, no 160).
    /// </summary>
    [TestClass]
    public class SqlServerTypesLoaderTests
    {
        /// <summary>Folder the test project copies MonogameTestbed's native DLLs into.</summary>
        private static string FixtureNativeFolder =>
            Path.Combine(AppContext.BaseDirectory, "LoaderFixture", "SqlServerTypes", IntPtr.Size > 4 ? "x64" : "x86");

        private static void Load(string rootApplicationPath) => SqlServerTypesLoader.Loader.LoadNativeAssemblies(rootApplicationPath);

        /// <summary>
        /// Creates an application root with a SqlServerTypes native folder holding only the named DLLs.
        /// </summary>
        private static string CreateRoot(params string[] dllNames)
        {
            string root = Path.Combine(Path.GetTempPath(), "SqlServerTypesLoaderTests", Guid.NewGuid().ToString("N"));
            string nativeFolder = Path.Combine(root, "SqlServerTypes", IntPtr.Size > 4 ? "x64" : "x86");
            Directory.CreateDirectory(nativeFolder);
            foreach (string name in dllNames)
                File.Copy(Path.Combine(FixtureNativeFolder, name), Path.Combine(nativeFolder, name));
            return root;
        }

        private static void AssertLoadFailsNaming(string root, string dllName)
        {
            try
            {
                Load(root);
            }
            catch (Exception e)
            {
                StringAssert.Contains(e.Message, dllName);
                return;
            }

            Assert.Fail($"Expected loading from {root} to fail on {dllName}");
        }

        [TestMethod]
        public void LoadsMonogameTestbedDllSetWithoutSpatial160() => Load(CreateRoot("msvcr120.dll", "SqlServerSpatial140.dll"));

        [TestMethod]
        public void MissingNativeFolderFailsOnRuntimeDll()
        {
            string root = Path.Combine(Path.GetTempPath(), "SqlServerTypesLoaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            AssertLoadFailsNaming(root, "msvcr120.dll");
        }

        [TestMethod]
        public void MissingSpatialDllFailsNamingSpatial140() => AssertLoadFailsNaming(CreateRoot("msvcr120.dll"), "SqlServerSpatial140.dll");
    }
}
