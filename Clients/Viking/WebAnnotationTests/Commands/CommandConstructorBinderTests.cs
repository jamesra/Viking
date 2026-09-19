using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Viking.UI.Commands;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Regression for linked-polygon enqueue: Activator cannot bind optional / interface
    /// constructors the way <see cref="CommandConstructorBinder"/> does.
    /// </summary>
    [TestClass]
    public class CommandConstructorBinderTests
    {
        /// <summary>
        /// Mirrors <c>SegmentationCommand</c>'s 8-parameter shape: required
        /// <see cref="IEnumerable{T}"/> pair plus optional trailing parameters, including <c>T?</c>.
        /// </summary>
        public sealed class SampleQueuedType
        {
            public IEnumerable<int> Foreground { get; }
            public IEnumerable<int> Background { get; }
            public string? Label { get; }
            public long? StructureTypeId { get; }
            public long? ExcludeId { get; }

            public SampleQueuedType(
                IEnumerable<int> foreground,
                IEnumerable<int> background,
                string? label = null,
                long? structureTypeId = null,
                long? excludeId = null)
            {
                Foreground = foreground;
                Background = background;
                Label = label;
                StructureTypeId = structureTypeId;
                ExcludeId = excludeId;
            }
        }

        [TestMethod]
        public void ActivatorRejectsOptionalTrailingArgsAndArrayForIEnumerable()
        {
            object[] args = [new int[] { 1, 2 }, Array.Empty<int>(), "ok", 7L];

            Assert.ThrowsException<MissingMethodException>(
                () => Activator.CreateInstance(typeof(SampleQueuedType), args));
        }

        [TestMethod]
        public void BinderAcceptsArrayForIEnumerableOptionalAndBoxedNullableLong()
        {
            object[] args = [new int[] { 1, 2 }, Array.Empty<int>(), "ok", 7L];

            var created = (SampleQueuedType)CommandConstructorBinder.Create(typeof(SampleQueuedType), args);

            CollectionAssert.AreEqual(new[] { 1, 2 }, created.Foreground as int[] ?? [.. created.Foreground]);
            Assert.AreEqual("ok", created.Label);
            Assert.AreEqual(7L, created.StructureTypeId);
            Assert.IsNull(created.ExcludeId);
        }

        [TestMethod]
        public void BinderFillsOmittedOptionalParametersFromDefaults()
        {
            object[] args = [new int[] { 9 }, Array.Empty<int>()];

            var created = (SampleQueuedType)CommandConstructorBinder.Create(typeof(SampleQueuedType), args);

            Assert.IsNull(created.Label);
            Assert.IsNull(created.StructureTypeId);
            Assert.IsNull(created.ExcludeId);
        }
    }
}
