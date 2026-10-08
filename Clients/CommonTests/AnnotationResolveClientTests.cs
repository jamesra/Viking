using System;
using System.Globalization;
using System.Reflection;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins <see cref="AnnotationResolveOptions"/> env/force gates, <see cref="AnnotationResolveClient.TryParseAnnotationRef"/>,
    /// and internal JSON parsing for Viking Test resolve navigation (no HTTP; reflection on <c>ParseResolveJson</c>).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class AnnotationResolveClientTests
    {
        static readonly MethodInfo ParseResolveJson = typeof(AnnotationResolveClient).GetMethod(
            "ParseResolveJson",
            BindingFlags.Static | BindingFlags.NonPublic);

        bool? _savedForceEnabled;
        string _savedEnvEnabled;
        string _savedEnvBase;

        [TestInitialize]
        public void SaveResolveEnvironment()
        {
            _savedForceEnabled = AnnotationResolveOptions.ForceEnabled;
            _savedEnvEnabled = Environment.GetEnvironmentVariable(AnnotationResolveOptions.EnvEnabled);
            _savedEnvBase = Environment.GetEnvironmentVariable(AnnotationResolveOptions.EnvBaseUrl);
            AnnotationResolveOptions.ForceEnabled = false;
            Environment.SetEnvironmentVariable(AnnotationResolveOptions.EnvEnabled, null);
            Environment.SetEnvironmentVariable(AnnotationResolveOptions.EnvBaseUrl, null);
        }

        [TestCleanup]
        public void RestoreResolveEnvironment()
        {
            if (_savedForceEnabled.HasValue)
                AnnotationResolveOptions.ForceEnabled = _savedForceEnabled.Value;
            Environment.SetEnvironmentVariable(AnnotationResolveOptions.EnvEnabled, _savedEnvEnabled);
            Environment.SetEnvironmentVariable(AnnotationResolveOptions.EnvBaseUrl, _savedEnvBase);
        }

        static AnnotationResolveResult InvokeParseResolveJson(string json)
        {
            Assert.IsNotNull(ParseResolveJson);
            try
            {
                return (AnnotationResolveResult)ParseResolveJson.Invoke(null, new object[] { json });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        [TestMethod]
        public void IsEnabled_DefaultEnvironment_IsFalse()
        {
            Assert.IsFalse(AnnotationResolveOptions.IsEnabled);
        }

        [TestMethod]
        public void IsEnabled_ForceEnabled_IsTrue()
        {
            AnnotationResolveOptions.ForceEnabled = true;
            Assert.IsTrue(AnnotationResolveOptions.IsEnabled);
        }

        [TestMethod]
        public void IsEnabled_EnvEnabledTruthyStrings_AreTrue()
        {
            foreach (string value in new[] { "1", "true", "TRUE", "yes", " Yes " })
            {
                Environment.SetEnvironmentVariable(AnnotationResolveOptions.EnvEnabled, value);
                Assert.IsTrue(AnnotationResolveOptions.IsEnabled, $"Expected enabled for '{value}'");
            }
        }

        [TestMethod]
        public void IsEnabled_EnvEnabledEmptyOrNonTruthy_IsFalse()
        {
            foreach (string value in new[] { "", "   ", "0", "false", "no", "maybe" })
            {
                Environment.SetEnvironmentVariable(AnnotationResolveOptions.EnvEnabled, value);
                Assert.IsFalse(AnnotationResolveOptions.IsEnabled, $"Expected disabled for '{value}'");
            }
        }

        [TestMethod]
        public void IsEnabled_EnvBaseUrlSetEvenWithoutResolveFlag_IsTrue()
        {
            Environment.SetEnvironmentVariable(AnnotationResolveOptions.EnvBaseUrl, "http://resolve.example/");
            Assert.IsTrue(AnnotationResolveOptions.IsEnabled);
        }

        [TestMethod]
        public void BaseUrl_NoEnv_ReturnsDefaultWithoutTrailingSlash()
        {
            Assert.AreEqual(AnnotationResolveOptions.DefaultBaseUrl, AnnotationResolveOptions.BaseUrl);
        }

        [TestMethod]
        public void BaseUrl_EnvBaseUrl_TrimsWhitespaceAndTrailingSlash()
        {
            Environment.SetEnvironmentVariable(AnnotationResolveOptions.EnvBaseUrl, "  http://host:9999/path/  ");
            Assert.AreEqual("http://host:9999/path", AnnotationResolveOptions.BaseUrl);
        }

        [TestMethod]
        public void TryParseAnnotationRef_BareId_ReturnsId()
        {
            Assert.IsTrue(AnnotationResolveClient.TryParseAnnotationRef("42", out long id));
            Assert.AreEqual(42L, id);
        }

        [TestMethod]
        public void TryParseAnnotationRef_AnnotationPrefixVariants_ReturnId()
        {
            Assert.IsTrue(AnnotationResolveClient.TryParseAnnotationRef("@annotation 7", out long a));
            Assert.AreEqual(7L, a);
            Assert.IsTrue(AnnotationResolveClient.TryParseAnnotationRef("@annotation:8", out long b));
            Assert.AreEqual(8L, b);
            Assert.IsTrue(AnnotationResolveClient.TryParseAnnotationRef("@ANNOTATION: 9", out long c));
            Assert.AreEqual(9L, c);
        }

        [TestMethod]
        public void TryParseAnnotationRef_WhitespacePaddedBareId_ReturnsId()
        {
            Assert.IsTrue(AnnotationResolveClient.TryParseAnnotationRef("  1001  ", out long id));
            Assert.AreEqual(1001L, id);
        }

        [TestMethod]
        public void TryParseAnnotationRef_ZeroNegativeOrNonNumeric_ReturnsFalse()
        {
            Assert.IsFalse(AnnotationResolveClient.TryParseAnnotationRef("0", out _));
            Assert.IsFalse(AnnotationResolveClient.TryParseAnnotationRef("-5", out _));
            Assert.IsFalse(AnnotationResolveClient.TryParseAnnotationRef("12abc", out _));
            Assert.IsFalse(AnnotationResolveClient.TryParseAnnotationRef("@annotation", out _));
            Assert.IsFalse(AnnotationResolveClient.TryParseAnnotationRef(null, out _));
            Assert.IsFalse(AnnotationResolveClient.TryParseAnnotationRef("   ", out _));
        }

        [TestMethod]
        public void TryParseAnnotationRef_PositiveIds_RoundTripBareAndPrefixed()
        {
            var positiveId = Arb.Default.PositiveInt().Generator
                .Select(i => (long)i.Get)
                .Where(id => id <= long.MaxValue)
                .ToArbitrary();

            Prop.ForAll(positiveId, id =>
            {
                string bare = id.ToString(CultureInfo.InvariantCulture);
                if (!AnnotationResolveClient.TryParseAnnotationRef(bare, out long parsedBare) || parsedBare != id)
                    return false;
                string prefixed = "@annotation:" + bare;
                return AnnotationResolveClient.TryParseAnnotationRef(prefixed, out long parsedPrefixed)
                    && parsedPrefixed == id;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void ParseResolveJson_SampleWithCenterAndBBox_ParsesFields()
        {
            const string json = @"{
  ""locationId"": 10,
  ""structureId"": 20,
  ""section"": 3,
  ""volume"": ""RC1"",
  ""center"": { ""x"": 1.5, ""y"": 2.25 },
  ""bbox"": { ""minX"": 0, ""minY"": 1, ""maxX"": 4, ""maxY"": 5 }
}";
            AnnotationResolveResult result = InvokeParseResolveJson(json);
            Assert.AreEqual(10L, result.LocationId);
            Assert.AreEqual(20L, result.StructureId);
            Assert.AreEqual(3, result.Section);
            Assert.AreEqual("RC1", result.Volume);
            Assert.AreEqual(1.5, result.Center.X, 1e-9);
            Assert.AreEqual(2.25, result.Center.Y, 1e-9);
            Assert.AreEqual(0, result.BBox.MinX, 1e-9);
            Assert.AreEqual(4, result.BBox.MaxX, 1e-9);
            Assert.AreEqual(4, result.BBox.Width, 1e-9);
        }

        [TestMethod]
        public void ParseResolveJson_MissingCenter_ThrowsFormatException()
        {
            const string json = @"{ ""locationId"": 1, ""structureId"": 2, ""section"": 1, ""volume"": ""v"" }";
            Assert.ThrowsException<FormatException>(() => InvokeParseResolveJson(json));
        }

        [TestMethod]
        public void ParseResolveJson_EmptyBody_ThrowsFormatException()
        {
            Assert.ThrowsException<FormatException>(() => InvokeParseResolveJson("   "));
        }

        [TestMethod]
        public void ParseResolveJson_MissingBBox_LeavesDefaultBBox()
        {
            const string json = @"{
  ""locationId"": 1,
  ""structureId"": 2,
  ""section"": 1,
  ""volume"": ""v"",
  ""center"": { ""x"": 0, ""y"": 0 }
}";
            AnnotationResolveResult result = InvokeParseResolveJson(json);
            Assert.AreEqual(0, result.BBox.MinX, 1e-9);
            Assert.AreEqual(0, result.BBox.Width, 1e-9);
        }
    }
}
