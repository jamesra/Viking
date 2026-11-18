using System;
using System.Collections.Generic;
using System.Net.Http;
using Grpc.Core;
using NUnit.Framework;
using WebAnnotationModel.gRPC;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// <see cref="ServerLoadErrors"/>: a failed annotation load becomes one status-bar line with the server's gRPC detail, a
    /// repeat within the interval is raised once, and a cancelled request is not reported.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ServerLoadErrorsTests
    {
        private readonly List<string> raised = new();
        private DateTime now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        private Func<DateTime> savedClock;

        [SetUp]
        public void Setup()
        {
            raised.Clear();
            savedClock = ServerLoadErrors.UtcNow;
            ServerLoadErrors.UtcNow = () => now;
            ServerLoadErrors.ResetForTests();
            ServerLoadErrors.Failed += raised.Add;
        }

        [TearDown]
        public void Cleanup()
        {
            ServerLoadErrors.Failed -= raised.Add;
            ServerLoadErrors.UtcNow = savedClock;
            ServerLoadErrors.ResetForTests();
        }

        private static RpcException Rpc(StatusCode code, string detail) => new(new Status(code, detail));

        [Test]
        public void Describe_ServerError_GivesTheDetailOnOneLine()
        {
            var e = Rpc(StatusCode.Internal, "The code generation directory cannot be created\r\nor set up correctly");
            Assert.AreEqual("Server error: The code generation directory cannot be created or set up correctly.",
                            ServerLoadErrors.Describe(e));
        }

        [Test]
        public void Describe_ServerErrorWithoutDetail_NamesTheStatus()
        {
            Assert.AreEqual("Server error: Internal.", ServerLoadErrors.Describe(Rpc(StatusCode.Internal, "")));
        }

        [Test]
        public void Describe_TimeoutUnreachableAndDenied_SayWhich()
        {
            Assert.AreEqual("The server did not answer in time.", ServerLoadErrors.Describe(Rpc(StatusCode.DeadlineExceeded, "x")));
            Assert.AreEqual("The annotation server could not be reached: no connection.",
                            ServerLoadErrors.Describe(Rpc(StatusCode.Unavailable, "no connection")));
            Assert.AreEqual("The server denied access: no read right.",
                            ServerLoadErrors.Describe(Rpc(StatusCode.PermissionDenied, "no read right")));
            Assert.AreEqual("The annotation server could not be reached: refused.",
                            ServerLoadErrors.Describe(new HttpRequestException("refused")));
        }

        [Test]
        public void Describe_LongDetail_IsTruncated()
        {
            string text = ServerLoadErrors.Describe(Rpc(StatusCode.Internal, new string('a', 1000)));
            Assert.LessOrEqual(text.Length, 240);
            Assert.IsTrue(text.EndsWith("…", StringComparison.Ordinal));
        }

        [Test]
        public void Report_NamesTheSectionAndWhatFailed()
        {
            ServerLoadErrors.Report("annotations", 812, Rpc(StatusCode.Internal, "boom"));
            CollectionAssert.AreEqual(new[] { "Section 812: annotations did not load. Server error: boom." }, raised);
        }

        [Test]
        public void Report_SameMessageWithinInterval_IsRaisedOnce()
        {
            ServerLoadErrors.Report("annotations", 812, Rpc(StatusCode.Internal, "boom"));
            now += TimeSpan.FromSeconds(5);
            ServerLoadErrors.Report("annotations", 812, Rpc(StatusCode.Internal, "boom"));
            Assert.AreEqual(1, raised.Count);

            ServerLoadErrors.Report("annotations", 813, Rpc(StatusCode.Internal, "boom"));
            Assert.AreEqual(2, raised.Count, "another section is another message");

            now += ServerLoadErrors.RepeatInterval;
            ServerLoadErrors.Report("annotations", 813, Rpc(StatusCode.Internal, "boom"));
            Assert.AreEqual(3, raised.Count, "after the interval the same message is raised again");
        }

        [Test]
        public void Report_CancelledRequest_IsNotAFailure()
        {
            ServerLoadErrors.Report("annotations", 1, new OperationCanceledException());
            ServerLoadErrors.Report("annotations", 1, Rpc(StatusCode.Cancelled, "client cancelled"));
            ServerLoadErrors.Report("annotations", 1, new AggregateException(Rpc(StatusCode.Cancelled, "x")));
            ServerLoadErrors.Report("annotations", 1, new ObjectDisposedException("channel"));
            Assert.AreEqual(0, raised.Count);
        }
    }
}
