using System;
using System.Linq;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins <see cref="StructureOrLabelMatch"/>, the free-text structure id / label filter shared by the
    /// review change feed and its OData query builder: tokenization on commas, semicolons and whitespace,
    /// case-insensitive substring matching on label and id text, and an empty query matching everything.
    /// </summary>
    [TestClass]
    public class StructureOrLabelMatchTests
    {
        [TestMethod]
        public void Tokenize_SplitsOnCommaSemicolonAndWhitespaceAndDropsEmpties()
        {
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e" },
                StructureOrLabelMatch.Tokenize(" a,b;;c\td\r\n e ,").ToArray());
        }

        [TestMethod]
        public void Tokenize_NullOrBlankQuery_IsEmpty()
        {
            Assert.AreEqual(0, StructureOrLabelMatch.Tokenize(null).Count);
            Assert.AreEqual(0, StructureOrLabelMatch.Tokenize("").Count);
            Assert.AreEqual(0, StructureOrLabelMatch.Tokenize(" \t ").Count);
        }

        [TestMethod]
        public void Matches_NullOrBlankQuery_MatchesEverythingEvenWithNoIdOrLabel()
        {
            Assert.IsTrue(StructureOrLabelMatch.Matches(null, null, null));
            Assert.IsTrue(StructureOrLabelMatch.Matches(null, null, ""));
            Assert.IsTrue(StructureOrLabelMatch.Matches(7, "axon", "  \t"));
        }

        [TestMethod]
        public void Matches_SeparatorOnlyQuery_HasNoTokensSoMatchesEverything()
        {
            // Regression: "," or ";" alone used to reject every row, while ReviewChangeODataQuery.BuildStructureFilter
            // treats the same text as no filter.
            Assert.IsTrue(StructureOrLabelMatch.Matches(7, "axon", ","));
            Assert.IsTrue(StructureOrLabelMatch.Matches(null, null, " ; ,	"));
        }

        [TestMethod]
        public void Matches_LabelIsCaseInsensitiveSubstring()
        {
            Assert.IsTrue(StructureOrLabelMatch.Matches(1, "Mitochondria", "CHONDR"));
            Assert.IsFalse(StructureOrLabelMatch.Matches(1, "Mitochondria", "nucleus"));
        }

        [TestMethod]
        public void Matches_IdIsSubstringOfIdText()
        {
            Assert.IsTrue(StructureOrLabelMatch.Matches(12345, null, "234"));
            Assert.IsFalse(StructureOrLabelMatch.Matches(12345, null, "246"));
        }

        [TestMethod]
        public void Matches_NegativeIdMatchesItsOwnText()
        {
            Assert.IsTrue(StructureOrLabelMatch.Matches(-5, null, "-5"));
            Assert.IsFalse(StructureOrLabelMatch.Matches(5, null, "-5"));
        }

        [TestMethod]
        public void Matches_NoIdAndNoLabel_NeverMatchesANonBlankQuery()
        {
            Assert.IsFalse(StructureOrLabelMatch.Matches(null, null, "1"));
            Assert.IsFalse(StructureOrLabelMatch.Matches(null, null, "a,b"));
        }

        [TestMethod]
        public void Matches_AnyTokenSufficesAndOneMissDoesNotRejectOthers()
        {
            Assert.IsTrue(StructureOrLabelMatch.Matches(10, "c1", "nomatch, c1"));
            Assert.IsTrue(StructureOrLabelMatch.Matches(10, "c1", "zzz;10"));
            Assert.IsFalse(StructureOrLabelMatch.Matches(10, "c1", "zzz yyy"));
        }

        /// <summary>Slow reference: any token that is a substring (ordinal, ignore case) of the id text or the label.</summary>
        private static bool Reference(long? id, string label, string query)
        {
            var tokens = StructureOrLabelMatch.Tokenize(query);
            if (tokens.Count == 0) return true;
            string idText = id.HasValue ? id.Value.ToString() : "";
            string labelText = label ?? "";
            return tokens.Any(t => idText.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0
                                   || labelText.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [TestMethod]
        public void Matches_Property_AgreesWithSubstringReference()
        {
            var ids = Gen.OneOf(Gen.Constant<long?>(null), Arb.Default.Int64().Generator.Select(i => (long?)i));
            var words = Gen.Elements("a", "B", "10", "-3", "ax", "AXON", "9", ",", ";", " ", "\t");
            var text = Gen.ListOf(words).Select(l => string.Concat(l));
            Prop.ForAll(Arb.From(ids), Arb.From(text), Arb.From(text), (id, label, query) =>
                StructureOrLabelMatch.Matches(id, label, query) == Reference(id, label, query))
                .QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void Matches_Property_ExactIdAlwaysMatches()
        {
            Prop.ForAll(Arb.Default.Int64(), id =>
                StructureOrLabelMatch.Matches(id, null, id.ToString())).QuickCheckThrowOnFailure();
        }
    }
}
