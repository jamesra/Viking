using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using Viking.GrpcSectionCorrectionService;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Pins which volume-load failures <see cref="SectionCorrectionsService.IsTransientVolumeFailure"/> treats as
    /// transient (CorrectStructures answers UNAVAILABLE). A refused stos/section HTTP load faults a task and
    /// arrives wrapped in an <see cref="AggregateException"/> from <c>Task.Result</c>, which the old type-only
    /// filter let escape as UNKNOWN.
    /// </summary>
    [TestClass]
    public sealed class TransientVolumeFailureTests
    {
        static readonly SocketException Refused = new((int)SocketError.ConnectionRefused);

        [TestMethod]
        public void DirectConnectivityFailuresAreTransient()
        {
            Assert.IsTrue(SectionCorrectionsService.IsTransientVolumeFailure(new WebException("x"), CancellationToken.None));
            Assert.IsTrue(SectionCorrectionsService.IsTransientVolumeFailure(new HttpRequestException("x", Refused), CancellationToken.None));
            Assert.IsTrue(SectionCorrectionsService.IsTransientVolumeFailure(Refused, CancellationToken.None));
            Assert.IsTrue(SectionCorrectionsService.IsTransientVolumeFailure(new IOException("x"), CancellationToken.None));
        }

        [TestMethod]
        public void AggregateWrappedConnectionRefusedIsTransient()
        {
            AggregateException wrapped = new(new HttpRequestException("refused", Refused));
            Assert.IsTrue(SectionCorrectionsService.IsTransientVolumeFailure(wrapped, CancellationToken.None));
        }

        [TestMethod]
        public void InnerSocketExceptionUnderOtherWrapperIsTransient()
        {
            Exception wrapped = new InvalidOperationException("load failed", Refused);
            Assert.IsTrue(SectionCorrectionsService.IsTransientVolumeFailure(wrapped, CancellationToken.None));
        }

        [TestMethod]
        public void HttpTimeoutIsTransientButCallerCancellationIsNot()
        {
            Assert.IsTrue(SectionCorrectionsService.IsTransientVolumeFailure(
                new AggregateException(new TaskCanceledExceptionStub()), CancellationToken.None));

            using CancellationTokenSource cts = new();
            cts.Cancel();
            Assert.IsFalse(SectionCorrectionsService.IsTransientVolumeFailure(new OperationCanceledException(cts.Token), cts.Token));
        }

        [TestMethod]
        public void UnrelatedFaultsAreNotTransient()
        {
            Assert.IsFalse(SectionCorrectionsService.IsTransientVolumeFailure(new FormatException("bad stos"), CancellationToken.None));
            Assert.IsFalse(SectionCorrectionsService.IsTransientVolumeFailure(new AggregateException(new FormatException("bad")), CancellationToken.None));
        }

        sealed class TaskCanceledExceptionStub : System.Threading.Tasks.TaskCanceledException
        {
        }
    }
}
