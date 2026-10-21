using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Services.Grpc;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="ShutdownCancellationInterceptor"/>: every call made through an intercepted invoker must be
    /// cancelled by the shutdown token, with or without a token of its own.
    /// </summary>
    [TestClass]
    public class ShutdownCancellationInterceptorTests
    {
        private static readonly Method<string, string> UnaryMethod = new(
            MethodType.Unary, "svc", "Unary", Marshallers.StringMarshaller, Marshallers.StringMarshaller);

        private static readonly Method<string, string> DuplexMethod = new(
            MethodType.DuplexStreaming, "svc", "Duplex", Marshallers.StringMarshaller, Marshallers.StringMarshaller);

        [TestMethod]
        public void CallWithoutToken_GetsShutdownToken()
        {
            using var shutdown = new CancellationTokenSource();
            var inner = new RecordingInvoker();
            CallInvoker invoker = inner.Intercept(new ShutdownCancellationInterceptor(shutdown.Token));

            invoker.AsyncUnaryCall(UnaryMethod, null, new CallOptions(), "request");

            Assert.IsFalse(inner.LastOptions.CancellationToken.IsCancellationRequested);
            shutdown.Cancel();
            Assert.IsTrue(inner.LastOptions.CancellationToken.IsCancellationRequested);
        }

        [TestMethod]
        public void CallWithOwnToken_IsCancelledByShutdown()
        {
            using var shutdown = new CancellationTokenSource();
            using var own = new CancellationTokenSource();
            var inner = new RecordingInvoker();
            CallInvoker invoker = inner.Intercept(new ShutdownCancellationInterceptor(shutdown.Token));

            invoker.AsyncUnaryCall(UnaryMethod, null, new CallOptions(cancellationToken: own.Token), "request");

            shutdown.Cancel();
            Assert.IsTrue(inner.LastOptions.CancellationToken.IsCancellationRequested);
        }

        [TestMethod]
        public void CallWithOwnToken_IsStillCancelledByOwnToken()
        {
            using var shutdown = new CancellationTokenSource();
            using var own = new CancellationTokenSource();
            var inner = new RecordingInvoker();
            CallInvoker invoker = inner.Intercept(new ShutdownCancellationInterceptor(shutdown.Token));

            invoker.AsyncUnaryCall(UnaryMethod, null, new CallOptions(cancellationToken: own.Token), "request");

            own.Cancel();
            Assert.IsTrue(inner.LastOptions.CancellationToken.IsCancellationRequested);
            Assert.IsFalse(shutdown.IsCancellationRequested);
        }

        [TestMethod]
        public void CallStartedAfterShutdown_StartsCancelled()
        {
            using var shutdown = new CancellationTokenSource();
            shutdown.Cancel();
            var inner = new RecordingInvoker();
            CallInvoker invoker = inner.Intercept(new ShutdownCancellationInterceptor(shutdown.Token));

            invoker.AsyncUnaryCall(UnaryMethod, null, new CallOptions(), "request");

            Assert.IsTrue(inner.LastOptions.CancellationToken.IsCancellationRequested);
        }

        [TestMethod]
        public void OtherOptions_ArePreserved()
        {
            using var shutdown = new CancellationTokenSource();
            var inner = new RecordingInvoker();
            CallInvoker invoker = inner.Intercept(new ShutdownCancellationInterceptor(shutdown.Token));
            DateTime deadline = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var headers = new Metadata { { "key", "value" } };

            invoker.AsyncUnaryCall(UnaryMethod, null, new CallOptions(headers, deadline), "request");

            Assert.AreEqual(deadline, inner.LastOptions.Deadline);
            Assert.AreSame(headers, inner.LastOptions.Headers);
        }

        [TestMethod]
        public async Task CompletedUnaryCall_ShutdownAfterwardsDoesNotThrow()
        {
            using var shutdown = new CancellationTokenSource();
            using var own = new CancellationTokenSource();
            var inner = new RecordingInvoker();
            CallInvoker invoker = inner.Intercept(new ShutdownCancellationInterceptor(shutdown.Token));

            AsyncUnaryCall<string> call = invoker.AsyncUnaryCall(UnaryMethod, null, new CallOptions(cancellationToken: own.Token), "request");
            inner.Response.SetResult("response");
            Assert.AreEqual("response", await call.ResponseAsync);

            // The linked source is disposed once the response completes; cancelling the inputs must stay harmless.
            shutdown.Cancel();
            own.Cancel();
        }

        [TestMethod]
        public void DuplexCall_DisposeReachesInnerCall()
        {
            using var shutdown = new CancellationTokenSource();
            using var own = new CancellationTokenSource();
            var inner = new RecordingInvoker();
            CallInvoker invoker = inner.Intercept(new ShutdownCancellationInterceptor(shutdown.Token));

            AsyncDuplexStreamingCall<string, string> call =
                invoker.AsyncDuplexStreamingCall(DuplexMethod, null, new CallOptions(cancellationToken: own.Token));
            call.Dispose();

            Assert.IsTrue(inner.DuplexDisposed);
            shutdown.Cancel();
        }

        [TestMethod]
        public void DuplexCall_IsCancelledByShutdown()
        {
            using var shutdown = new CancellationTokenSource();
            using var own = new CancellationTokenSource();
            var inner = new RecordingInvoker();
            CallInvoker invoker = inner.Intercept(new ShutdownCancellationInterceptor(shutdown.Token));

            using AsyncDuplexStreamingCall<string, string> call =
                invoker.AsyncDuplexStreamingCall(DuplexMethod, null, new CallOptions(cancellationToken: own.Token));

            shutdown.Cancel();
            Assert.IsTrue(inner.LastOptions.CancellationToken.IsCancellationRequested);
        }

        /// <summary>
        /// Records the options of the last call and returns calls that complete only when the test says so.
        /// </summary>
        private sealed class RecordingInvoker : CallInvoker
        {
            public CallOptions LastOptions { get; private set; }

            public TaskCompletionSource<string> Response { get; } = new();

            public bool DuplexDisposed { get; private set; }

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            {
                LastOptions = options;
                return default!;
            }

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            {
                LastOptions = options;
                return new AsyncUnaryCall<TResponse>(
                    Response.Task.ContinueWith(t => (TResponse)(object)t.Result, TaskScheduler.Default),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => { });
            }

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            {
                LastOptions = options;
                return new AsyncServerStreamingCall<TResponse>(
                    null!, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
            }

            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string host, CallOptions options)
            {
                LastOptions = options;
                return new AsyncClientStreamingCall<TRequest, TResponse>(
                    null!, new TaskCompletionSource<TResponse>().Task, Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess, () => new Metadata(), () => { });
            }

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
                Method<TRequest, TResponse> method, string host, CallOptions options)
            {
                LastOptions = options;
                return new AsyncDuplexStreamingCall<TRequest, TResponse>(
                    null!, null!, Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess, () => new Metadata(), () => DuplexDisposed = true);
            }
        }
    }
}
