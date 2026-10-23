using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Viking.SectionCorrectionBuilder;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Pins <see cref="SqlMorphologyLoader.AnnotationWatermark"/>, the value CorrectStructures returns as
    /// annotation_watermark so a client can tell its cached offsets predate an annotation edit.
    /// </summary>
    [TestClass]
    public class AnnotationWatermarkTests
    {
        static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        static SqlMorphologyLoader.CandidateRow Location(long id, long z, DateTime modified) =>
            new() { ID = id, ParentID = 7, Z = z, LastModified = modified };

        static SqlMorphologyLoader.LinkRow Link(long a, long b, DateTime created) =>
            new() { A = a, B = b, Created = created };

        [TestMethod]
        public void NoRows_IsNull() =>
            Assert.IsNull(SqlMorphologyLoader.AnnotationWatermark([], []));

        [TestMethod]
        public void NewestLocationEdit_IsTheWatermark()
        {
            DateTime? watermark = SqlMorphologyLoader.AnnotationWatermark(
                [Location(1, 10, T0), Location(2, 11, T0.AddHours(3))],
                [Link(1, 2, T0.AddHours(1))]);
            Assert.AreEqual(T0.AddHours(3), watermark);
        }

        /// <summary>Linking two existing locations changes neither row's LastModified; the link's Created must count.</summary>
        [TestMethod]
        public void NewLink_MovesTheWatermark()
        {
            DateTime? watermark = SqlMorphologyLoader.AnnotationWatermark(
                [Location(1, 10, T0), Location(2, 11, T0)],
                [Link(1, 2, T0.AddDays(2))]);
            Assert.AreEqual(T0.AddDays(2), watermark);
        }

        /// <summary>
        /// The known limit, pinned so nobody relies on it for deletes: removing a location that was not the newest edit
        /// leaves the watermark where it was, although the corrected positions may change.
        /// </summary>
        [TestMethod]
        public void DeletedLocation_DoesNotMoveTheWatermark()
        {
            List<SqlMorphologyLoader.CandidateRow> before = [Location(1, 10, T0), Location(2, 11, T0.AddHours(1)), Location(3, 12, T0.AddHours(5))];
            List<SqlMorphologyLoader.CandidateRow> after = [Location(1, 10, T0), Location(3, 12, T0.AddHours(5))];
            Assert.AreEqual(
                SqlMorphologyLoader.AnnotationWatermark(before, []),
                SqlMorphologyLoader.AnnotationWatermark(after, []));
        }
    }
}
