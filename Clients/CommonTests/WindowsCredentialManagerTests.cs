using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Viking.Services;

namespace CommonTests
{
    /// <summary>
    /// Pins the text format that <see cref="WindowsCredentialManager"/> stores inside its DPAPI blob.
    /// LoginViewModel saves the typed password through it, so a password containing the '|' field
    /// separator must come back unchanged instead of being cut short or failing to load.
    /// </summary>
    [TestClass]
    public class WindowsCredentialManagerTests
    {
        static readonly DateTime SavedAt = new DateTime(2026, 10, 21, 12, 30, 0, DateTimeKind.Utc);

        static WindowsCredentialManager.CredentialData RoundTrip(string user, string password, string url)
        {
            var data = new WindowsCredentialManager.CredentialData { Username = user, Password = password, ServerUrl = url, SavedAt = SavedAt };
            return WindowsCredentialManager.DeserializeCredentialData(WindowsCredentialManager.SerializeCredentialData(data));
        }

        [DataTestMethod]
        [DataRow("plain")]
        [DataRow("a|b")]
        [DataRow("|leading")]
        [DataRow("trailing|")]
        [DataRow("x||y|||z")]
        public void RoundTrip_PreservesPasswordIncludingSeparators(string password)
        {
            var result = RoundTrip("alice", password, "https://identity.codepharm.net:5001/");

            Assert.AreEqual("alice", result.Username);
            Assert.AreEqual(password, result.Password);
            Assert.AreEqual("https://identity.codepharm.net:5001/", result.ServerUrl);
            Assert.AreEqual(SavedAt, result.SavedAt.ToUniversalTime());
        }

        [TestMethod]
        public void RoundTrip_EmptyServerUrl_ReadsBackAsNull()
        {
            Assert.IsNull(RoundTrip("alice", "p|w", null).ServerUrl);
        }

        [TestMethod]
        public void Deserialize_TooFewFields_ThrowsFormatException()
        {
            Assert.ThrowsException<FormatException>(() => WindowsCredentialManager.DeserializeCredentialData("alice|pw|2026-10-21T12:30:00.0000000Z"));
        }
    }
}
