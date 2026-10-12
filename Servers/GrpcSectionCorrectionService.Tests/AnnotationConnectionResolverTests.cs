using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Viking.GrpcSectionCorrectionService;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Tests for <see cref="AnnotationConnectionResolver"/>: map and template precedence, blank handling,
    /// and the database-then-context-name lookup. All connection strings are fake placeholders.
    /// </summary>
    [TestClass]
    public class AnnotationConnectionResolverTests
    {
        const string FakeA = "Server=test;Database=a";
        const string FakeB = "Server=test;Database=b";

        [TestMethod]
        public void Resolve_MapIsCaseInsensitive()
        {
            var resolver = new AnnotationConnectionResolver(new Dictionary<string, string> { ["Rc1"] = FakeA }, null);
            Assert.AreEqual(FakeA, resolver.Resolve("RC1"));
            Assert.AreEqual(FakeA, resolver.Resolve("rc1"));
        }

        [TestMethod]
        public void Resolve_BlankKeysAndValuesAreDropped_AndFallThroughToTemplate()
        {
            var map = new Dictionary<string, string> { ["Blank"] = "  ", [" "] = FakeA, ["Good"] = FakeB };
            var resolver = new AnnotationConnectionResolver(map, "Server=test;Database={VolumeName}");
            Assert.AreEqual("Server=test;Database=Blank", resolver.Resolve("Blank"));
            Assert.AreEqual(FakeB, resolver.Resolve("Good"));
        }

        [TestMethod]
        public void Resolve_BlankValueWithoutTemplate_IsNull()
        {
            var resolver = new AnnotationConnectionResolver(new Dictionary<string, string> { ["Blank"] = "" }, null);
            Assert.IsNull(resolver.Resolve("Blank"));
        }

        [TestMethod]
        public void Resolve_MapBeatsTemplate()
        {
            var resolver = new AnnotationConnectionResolver(new Dictionary<string, string> { ["RC1"] = FakeA }, "Server=test;Database={VolumeName}");
            Assert.AreEqual(FakeA, resolver.Resolve("RC1"));
            Assert.AreEqual("Server=test;Database=Other", resolver.Resolve("Other"));
        }

        [TestMethod]
        public void Resolve_TemplatePlaceholderReplacedCaseInsensitively()
        {
            var resolver = new AnnotationConnectionResolver(null, "Server=test;Database={volumename}");
            Assert.AreEqual("Server=test;Database=RC1", resolver.Resolve("RC1"));
        }

        [TestMethod]
        public void Resolve_BlankNameOrNothingConfigured_IsNull()
        {
            var resolver = new AnnotationConnectionResolver(null, "Server=test;Database={VolumeName}");
            Assert.IsNull(resolver.Resolve((string)null));
            Assert.IsNull(resolver.Resolve("  "));
            Assert.IsNull(new AnnotationConnectionResolver(null, null).Resolve("RC1"));
            Assert.IsNull(new AnnotationConnectionResolver(null, "  ").Resolve("RC1"));
        }

        [TestMethod]
        public void Resolve_DatabaseFirst_ThenContextNamesInOrder()
        {
            var map = new Dictionary<string, string> { ["Db"] = FakeA, ["Ctx1"] = FakeB, ["Ctx2"] = "Server=test;Database=c" };
            var resolver = new AnnotationConnectionResolver(map, null);

            Assert.AreEqual(FakeA, resolver.Resolve("Db", ["Ctx1", "Ctx2"]));
            Assert.AreEqual(FakeB, resolver.Resolve("Unknown", ["Missing", "Ctx1", "Ctx2"]));
            Assert.IsNull(resolver.Resolve("Unknown", ["Missing"]));
            Assert.IsNull(resolver.Resolve("Unknown", []));
        }

        [TestMethod]
        public void FromConfiguration_ReadsConnectionsAndTemplate()
        {
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["AnnotationConnections:RC1"] = FakeA,
                ["AnnotationConnections:Empty"] = "",
                ["AnnotationConnectionTemplate"] = "Server=test;Database={VolumeName}"
            }).Build();

            AnnotationConnectionResolver resolver = AnnotationConnectionResolver.FromConfiguration(config);

            Assert.AreEqual(FakeA, resolver.Resolve("rc1"));
            Assert.AreEqual("Server=test;Database=Empty", resolver.Resolve("Empty"));
        }

        [TestMethod]
        public void FromConfiguration_NothingConfigured_ResolvesNull()
        {
            AnnotationConnectionResolver resolver = AnnotationConnectionResolver.FromConfiguration(new ConfigurationBuilder().Build());
            Assert.IsNull(resolver.Resolve("RC1"));
        }
    }
}
