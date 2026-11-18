using System;
using System.Collections.Generic;
using System.ServiceModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// <see cref="ServerLoadErrors"/>: a failed annotation load becomes one status-bar line with the server's detail, a
    /// repeat within the interval is raised once, and a cancelled request is not reported.
    /// </summary>
    [TestClass]
    public class ServerLoadErrorsTests
    {
        private readonly List<string> raised = [];
        private DateTime now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        private Func<DateTime> savedClock = null!;

        [TestInitialize]
        public void Setup()
        {
            savedClock = ServerLoadErrors.UtcNow;
            ServerLoadErrors.UtcNow = () => now;
            ServerLoadErrors.ResetForTests();
            ServerLoadErrors.Failed += raised.Add;
        }

        [TestCleanup]
        public void Cleanup()
        {
            ServerLoadErrors.Failed -= raised.Add;
            ServerLoadErrors.UtcNow = savedClock;
            ServerLoadErrors.ResetForTests();
        }

        [TestMethod]
        public void Describe_ServerFault_GivesTheServerMessageOnOneLine()
        {
            var fault = new FaultException(new FaultReason("The code generation directory cannot be created\r\nor set up correctly"));
            Assert.AreEqual("Server error: The code generation directory cannot be created or set up correctly.",
                            ServerLoadErrors.Describe(fault));
        }

        [TestMethod]
        public void Describe_FaultWithExceptionDetail_UsesTheDetail()
        {
            var detail = new ExceptionDetail(new InvalidOperationException("SQL failed."));
            var fault = new FaultException<ExceptionDetail>(detail, new FaultReason("outer"));
            Assert.AreEqual("Server error: SQL failed.", ServerLoadErrors.Describe(fault));
        }

        [TestMethod]
        public void Describe_TimeoutAndUnreachable_SayWhich()
        {
            Assert.AreEqual("The server did not answer in time.", ServerLoadErrors.Describe(new TimeoutException("x")));
            Assert.AreEqual("The annotation server could not be reached.", ServerLoadErrors.Describe(new EndpointNotFoundException("x")));
        }

        [TestMethod]
        public void Describe_LongMessage_IsTruncated()
        {
            string text = ServerLoadErrors.Describe(new FaultException(new string('a', 1000)));
            Assert.IsTrue(text.Length <= 240, text.Length.ToString());
            Assert.IsTrue(text.EndsWith("…", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Report_NamesTheSectionAndWhatFailed()
        {
            ServerLoadErrors.Report("annotations", 812, new FaultException("boom"));
            CollectionAssert.AreEqual(new[] { "Section 812: annotations did not load. Server error: boom." }, raised);
        }

        [TestMethod]
        public void Report_SameMessageWithinInterval_IsRaisedOnce()
        {
            ServerLoadErrors.Report("annotations", 812, new FaultException("boom"));
            now += TimeSpan.FromSeconds(5);
            ServerLoadErrors.Report("annotations", 812, new FaultException("boom"));
            Assert.AreEqual(1, raised.Count);

            ServerLoadErrors.Report("annotations", 813, new FaultException("boom"));
            Assert.AreEqual(2, raised.Count, "another section is another message");

            now += ServerLoadErrors.RepeatInterval;
            ServerLoadErrors.Report("annotations", 813, new FaultException("boom"));
            Assert.AreEqual(3, raised.Count, "after the interval the same message is raised again");
        }

        [TestMethod]
        public void Report_CancelledOrAbortedRequest_IsNotAFailure()
        {
            ServerLoadErrors.Report("annotations", 1, new OperationCanceledException());
            ServerLoadErrors.Report("annotations", 1, new CommunicationObjectAbortedException());
            ServerLoadErrors.Report("annotations", 1, new ObjectDisposedException("channel"));
            Assert.AreEqual(0, raised.Count);
        }
    }
}
