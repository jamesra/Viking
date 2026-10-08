using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins the string rules of <see cref="ResourceScopeNames"/>: spaces become '-' in scope tokens,
    /// TryParse splits at the LAST dot, and prefix collisions ignore case. The token and login code
    /// build and split scopes with these helpers, so a change here shifts every volume's scope name.
    /// Tests only the pure string helpers; no token handling is exercised.
    /// </summary>
    [TestClass]
    public class ResourceScopeNamesTests
    {
        private static readonly Arbitrary<string> NoDotNonEmptyName =
            Arb.Default.NonEmptyString().Generator
                .Select(s => s.Get)
                .Where(s => s.IndexOf('.') < 0)
                .ToArbitrary();

        [TestMethod]
        public void ToScope_PropertyThenTryParse_RecoversEncodedPrefixAndPermission()
        {
            Prop.ForAll(NoDotNonEmptyName, NoDotNonEmptyName, (volume, permission) =>
            {
                string scope = ResourceScopeNames.ToScope(volume, permission);
                bool ok = ResourceScopeNames.TryParse(scope, out string prefix, out string encoded);
                return ok
                    && prefix == ResourceScopeNames.ToScopePrefix(volume)
                    && encoded == ResourceScopeNames.ToScopePrefix(permission);
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void ToScopePrefix_Property_ResultHasNoSpacesAndIsIdempotent()
        {
            Prop.ForAll(Arb.Default.NonEmptyString(), s =>
            {
                string once = ResourceScopeNames.ToScopePrefix(s.Get);
                return once.IndexOf(' ') < 0 && ResourceScopeNames.ToScopePrefix(once) == once;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void ToPermissionId_Property_HyphenFreeNamesRoundTripThroughToScopePrefix()
        {
            var hyphenFree = Arb.Default.NonEmptyString().Generator
                .Select(s => s.Get)
                .Where(s => s.IndexOf('-') < 0)
                .ToArbitrary();
            Prop.ForAll(hyphenFree, name =>
                ResourceScopeNames.ToPermissionId(ResourceScopeNames.ToScopePrefix(name)) == name)
                .QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void ToPermissionId_HyphenInOriginalName_BecomesSpace()
        {
            Assert.AreEqual("Read Write", ResourceScopeNames.ToPermissionId("Read-Write"));
            Assert.AreEqual("a b", ResourceScopeNames.ToPermissionId(ResourceScopeNames.ToScopePrefix("a-b")));
        }

        [TestMethod]
        public void ToPermissionId_NullAndEmpty_ReturnedUnchanged()
        {
            Assert.IsNull(ResourceScopeNames.ToPermissionId(null));
            Assert.AreEqual(string.Empty, ResourceScopeNames.ToPermissionId(string.Empty));
        }

        [TestMethod]
        public void ToScopePrefix_NullAndEmpty_ReturnedUnchanged()
        {
            Assert.IsNull(ResourceScopeNames.ToScopePrefix(null));
            Assert.AreEqual(string.Empty, ResourceScopeNames.ToScopePrefix(string.Empty));
        }

        [TestMethod]
        public void ToScope_SpacesInVolumeAndPermission_AreEncoded()
        {
            Assert.AreEqual("Big-Volume.Add-Annotations", ResourceScopeNames.ToScope("Big Volume", "Add Annotations"));
        }

        [TestMethod]
        public void ToScope_PermissionIdWithSpaces_UsesHyphenSeparatedPermission()
        {
            Assert.AreEqual("Vol.Read-Only-Access", ResourceScopeNames.ToScope("Vol", "Read Only Access"));
        }

        [TestMethod]
        public void TryParse_ValidScope_SplitsAtDot()
        {
            Assert.IsTrue(ResourceScopeNames.TryParse("Vol.Read", out string prefix, out string permission));
            Assert.AreEqual("Vol", prefix);
            Assert.AreEqual("Read", permission);
        }

        [TestMethod]
        public void TryParse_MultipleDots_SplitsAtLastDot()
        {
            Assert.IsTrue(ResourceScopeNames.TryParse("Rabbit.Retina.v2.Read", out string prefix, out string permission));
            Assert.AreEqual("Rabbit.Retina.v2", prefix);
            Assert.AreEqual("Read", permission);
        }

        [TestMethod]
        public void TryParse_NullOrEmpty_FailsWithNullOutputs()
        {
            foreach (string scope in new string[] { null, string.Empty })
            {
                Assert.IsFalse(ResourceScopeNames.TryParse(scope, out string prefix, out string permission));
                Assert.IsNull(prefix);
                Assert.IsNull(permission);
            }
        }

        [TestMethod]
        public void TryParse_NoDot_Fails()
        {
            Assert.IsFalse(ResourceScopeNames.TryParse("VolRead", out string prefix, out string permission));
            Assert.IsNull(prefix);
            Assert.IsNull(permission);
        }

        [TestMethod]
        public void TryParse_LeadingDot_Fails()
        {
            Assert.IsFalse(ResourceScopeNames.TryParse(".Read", out _, out _));
        }

        [TestMethod]
        public void TryParse_TrailingDot_Fails()
        {
            Assert.IsFalse(ResourceScopeNames.TryParse("Vol.", out string prefix, out string permission));
            Assert.IsNull(prefix);
            Assert.IsNull(permission);
        }

        [TestMethod]
        public void TryParse_OnlyDot_Fails()
        {
            Assert.IsFalse(ResourceScopeNames.TryParse(".", out _, out _));
        }

        [TestMethod]
        public void TryParse_Property_SucceedsExactlyWhenLastDotIsInteriorAndRoundTrips()
        {
            Prop.ForAll(Arb.Default.String(), s =>
            {
                bool ok = ResourceScopeNames.TryParse(s, out string prefix, out string permission);
                int last = string.IsNullOrEmpty(s) ? -1 : s.LastIndexOf('.');
                bool expected = last > 0 && last < s.Length - 1;
                if (ok != expected)
                    return false;
                if (!ok)
                    return prefix == null && permission == null;
                return prefix + "." + permission == s && permission.IndexOf('.') < 0;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void ScopePrefixesCollide_NullOrEmpty_NeverCollides()
        {
            Assert.IsFalse(ResourceScopeNames.ScopePrefixesCollide(null, null));
            Assert.IsFalse(ResourceScopeNames.ScopePrefixesCollide(string.Empty, string.Empty));
            Assert.IsFalse(ResourceScopeNames.ScopePrefixesCollide("Vol", null));
            Assert.IsFalse(ResourceScopeNames.ScopePrefixesCollide(string.Empty, "Vol"));
        }

        [TestMethod]
        public void ScopePrefixesCollide_SpaceAndHyphenVariants_Collide()
        {
            Assert.IsTrue(ResourceScopeNames.ScopePrefixesCollide("Big Volume", "Big-Volume"));
        }

        [TestMethod]
        public void ScopePrefixesCollide_DifferentCaseAfterEncoding_Collides()
        {
            Assert.IsTrue(ResourceScopeNames.ScopePrefixesCollide("big volume", "BIG-VOLUME"));
        }

        [TestMethod]
        public void ScopePrefixesCollide_DifferentNames_DoNotCollide()
        {
            Assert.IsFalse(ResourceScopeNames.ScopePrefixesCollide("Big Volume", "Big Volumes"));
        }

        [TestMethod]
        public void ScopePrefixesCollide_Property_SymmetricAndTrueForEncodedSelf()
        {
            Prop.ForAll(Arb.Default.NonEmptyString(), Arb.Default.NonEmptyString(), (a, b) =>
            {
                bool forward = ResourceScopeNames.ScopePrefixesCollide(a.Get, b.Get);
                bool backward = ResourceScopeNames.ScopePrefixesCollide(b.Get, a.Get);
                bool self = ResourceScopeNames.ScopePrefixesCollide(a.Get, ResourceScopeNames.ToScopePrefix(a.Get));
                return forward == backward && self;
            }).QuickCheckThrowOnFailure();
        }
    }
}
