using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.SectionCorrection;

namespace SectionCorrection.Tests
{
    /// <summary>
    /// Pins <see cref="StructureFingerprint"/>, which CorrectStructures and GetStructureFingerprint return so a client can
    /// tell its cached corrected offsets are stale: every kind of annotation change must change the token.
    /// </summary>
    [TestClass]
    public class StructureFingerprintTests
    {
        static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        static readonly List<(long Id, DateTime Modified)> Locations =
            [(1, T0), (2, T0.AddHours(1)), (3, T0.AddHours(5))];

        static readonly List<(long A, long B, DateTime Created)> Links =
            [(1, 2, T0.AddHours(1)), (2, 3, T0.AddHours(5))];

        static string Token(IEnumerable<(long, DateTime)> locations, IEnumerable<(long, long, DateTime)> links) =>
            StructureFingerprint.FromRows(locations, links).Token;

        static readonly string Baseline = Token(Locations, Links);

        [TestMethod]
        public void SameRows_InAnyOrder_SameToken() =>
            Assert.AreEqual(Baseline, Token(Enumerable.Reverse(Locations), Enumerable.Reverse(Links)));

        [TestMethod]
        public void EditedLocation_ChangesToken() =>
            Assert.AreNotEqual(Baseline, Token([(1, T0), (2, T0.AddHours(9)), (3, T0.AddHours(5))], Links));

        [TestMethod]
        public void NewLocation_ChangesToken() =>
            Assert.AreNotEqual(Baseline, Token([.. Locations, (4, T0.AddHours(6))], Links));

        [TestMethod]
        public void NewLink_ChangesToken() =>
            Assert.AreNotEqual(Baseline, Token(Locations, [.. Links, (1, 3, T0.AddHours(6))]));

        /// <summary>
        /// The case the watermark misses: deleting a location that was not the newest edit leaves the newest time where
        /// it was, but the count drops.
        /// </summary>
        [TestMethod]
        public void DeletedLocation_ChangesToken_ThoughNotTheWatermark()
        {
            List<(long, DateTime)> after = [(1, T0), (3, T0.AddHours(5))];
            Assert.AreEqual(StructureFingerprint.FromRows(Locations, Links).Newest, StructureFingerprint.FromRows(after, Links).Newest,
                "precondition: the watermark cannot see this delete");
            Assert.AreNotEqual(Baseline, Token(after, Links));
        }

        [TestMethod]
        public void DeletedLink_ChangesToken_ThoughNotTheWatermark()
        {
            List<(long, long, DateTime)> after = [(2, 3, T0.AddHours(5))];
            Assert.AreEqual(StructureFingerprint.FromRows(Locations, Links).Newest, StructureFingerprint.FromRows(Locations, after).Newest,
                "precondition: the watermark cannot see this delete");
            Assert.AreNotEqual(Baseline, Token(Locations, after));
        }

        /// <summary>Linking two existing locations touches neither location's LastModified, so the link time must count.</summary>
        [TestMethod]
        public void Watermark_IsNewestOfLocationEditAndLinkCreation()
        {
            Assert.AreEqual(T0.AddDays(2), StructureFingerprint.FromRows(Locations, [(1, 2, T0.AddDays(2))]).Newest);
            Assert.AreEqual(T0.AddHours(5), StructureFingerprint.FromRows(Locations, []).Newest);
        }

        [TestMethod]
        public void NoRows_HasNoWatermark_AndAStableToken()
        {
            StructureFingerprint empty = StructureFingerprint.FromRows([], []);
            Assert.IsNull(empty.Newest);
            Assert.AreEqual(empty.Token, StructureFingerprint.FromRows([], []).Token);
            Assert.AreNotEqual(Baseline, empty.Token);
        }
    }
}
