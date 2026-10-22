using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Viking.Tokens;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="TokenErrorHelper.ToExceptionMessage"/>, which turns login failures into the text shown in the
    /// login window. On .NET Framework an <see cref="HttpRequestException"/> for an unreachable server carries a generic
    /// outer message and the real cause on the inner exception.
    /// </summary>
    [TestClass]
    public class TokenErrorHelperTests
    {
        private const string Friendly = "The identity server did not respond in time. Please check your network and try again.";

        /// <summary>The .NET Framework shape of a refused or unreachable connection: generic outer message, cause inside.</summary>
        [TestMethod]
        public void ToExceptionMessage_HttpRequestExceptionWithConnectInner_IsFriendly()
        {
            var ex = new HttpRequestException("An error occurred while sending the request.",
                new Exception("Unable to connect to the remote server"));
            Assert.AreEqual(Friendly, TokenErrorHelper.ToExceptionMessage(ex));
        }

        /// <summary>An HTTP status failure has no inner exception and keeps its status text.</summary>
        [TestMethod]
        public void ToExceptionMessage_HttpStatusFailure_KeepsStatus()
        {
            const string status = "Response status code does not indicate success: 404 (Not Found).";
            Assert.AreEqual(status, TokenErrorHelper.ToExceptionMessage(new HttpRequestException(status)));
        }

        /// <summary>Message-only connectivity failures (.NET Core style) stay friendly.</summary>
        [TestMethod]
        public void ToExceptionMessage_HttpRequestExceptionWithConnectionMessage_IsFriendly()
        {
            var ex = new HttpRequestException("No connection could be made because the target machine actively refused it.");
            Assert.AreEqual(Friendly, TokenErrorHelper.ToExceptionMessage(ex));
        }

        /// <summary>A canceled request is reported as a timeout.</summary>
        [TestMethod]
        public void ToExceptionMessage_TaskCanceled_IsFriendly()
        {
            Assert.AreEqual(Friendly, TokenErrorHelper.ToExceptionMessage(new TaskCanceledException()));
        }

        /// <summary>Other exceptions keep their own message; null gets a generic text.</summary>
        [TestMethod]
        public void ToExceptionMessage_OtherAndNull()
        {
            Assert.AreEqual("bad token", TokenErrorHelper.ToExceptionMessage(new InvalidOperationException("bad token")));
            Assert.AreEqual("An unexpected error occurred.", TokenErrorHelper.ToExceptionMessage(null));
        }
    }
}
