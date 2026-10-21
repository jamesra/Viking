using System.Threading;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Viking.Services.Grpc
{
    /// <summary>
    /// Adds a shutdown token, normally <see cref="ApplicationShutdown.Token"/>, to the cancellation token of every
    /// call made through an intercepted invoker, so closing Viking cancels calls still in flight.
    /// </summary>
    /// <remarks>
    /// Without this, a segmentation stream (300 s deadline) or a correction call (no deadline) kept running after the
    /// main window closed, and Grpc.Core's channel shutdown waited for it. Apply with
    /// <c>channel.Intercept(ShutdownCancellationInterceptor.Instance)</c> wherever a client is built: the segmentation
    /// session and the correction shim source. A call started after shutdown begins fails at once with
    /// <see cref="StatusCode.Cancelled"/>. When the caller passes its own cancellable token, a linked source is created
    /// per call and disposed when the call completes (unary) or is disposed (streaming), so streaming calls must be
    /// disposed, as they must be anyway.
    /// </remarks>
    public sealed class ShutdownCancellationInterceptor : Interceptor
    {
        private readonly CancellationToken _shutdown;

        /// <summary>The interceptor every Viking gRPC client uses, tied to <see cref="ApplicationShutdown.Token"/>.</summary>
        public static ShutdownCancellationInterceptor Instance { get; } = new(ApplicationShutdown.Token);

        /// <param name="shutdown">Token added to every call. Viking passes <see cref="ApplicationShutdown.Token"/>; tests pass their own.</param>
        public ShutdownCancellationInterceptor(CancellationToken shutdown)
        {
            _shutdown = shutdown;
        }

        /// <inheritdoc/>
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            CancellationTokenSource? linked = WithShutdownToken(ref context);
            try
            {
                return continuation(request, context);
            }
            finally
            {
                linked?.Dispose();
            }
        }

        /// <inheritdoc/>
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            CancellationTokenSource? linked = WithShutdownToken(ref context);
            AsyncUnaryCall<TResponse> call = continuation(request, context);
            if (linked is null)
                return call;

            call.ResponseAsync.ContinueWith(
                static (_, state) => ((CancellationTokenSource)state!).Dispose(),
                linked,
                CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                System.Threading.Tasks.TaskScheduler.Default);
            return call;
        }

        /// <inheritdoc/>
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            CancellationTokenSource? linked = WithShutdownToken(ref context);
            AsyncServerStreamingCall<TResponse> call = continuation(request, context);
            if (linked is null)
                return call;

            return new AsyncServerStreamingCall<TResponse>(
                call.ResponseStream,
                call.ResponseHeadersAsync,
                call.GetStatus,
                call.GetTrailers,
                () =>
                {
                    call.Dispose();
                    linked.Dispose();
                });
        }

        /// <inheritdoc/>
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            CancellationTokenSource? linked = WithShutdownToken(ref context);
            AsyncClientStreamingCall<TRequest, TResponse> call = continuation(context);
            if (linked is null)
                return call;

            return new AsyncClientStreamingCall<TRequest, TResponse>(
                call.RequestStream,
                call.ResponseAsync,
                call.ResponseHeadersAsync,
                call.GetStatus,
                call.GetTrailers,
                () =>
                {
                    call.Dispose();
                    linked.Dispose();
                });
        }

        /// <inheritdoc/>
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            CancellationTokenSource? linked = WithShutdownToken(ref context);
            AsyncDuplexStreamingCall<TRequest, TResponse> call = continuation(context);
            if (linked is null)
                return call;

            return new AsyncDuplexStreamingCall<TRequest, TResponse>(
                call.RequestStream,
                call.ResponseStream,
                call.ResponseHeadersAsync,
                call.GetStatus,
                call.GetTrailers,
                () =>
                {
                    call.Dispose();
                    linked.Dispose();
                });
        }

        /// <summary>
        /// Replaces <paramref name="context"/> with one whose token also fires on shutdown.
        /// </summary>
        /// <returns>The linked source the caller must dispose after the call, or null when none was needed.</returns>
        private CancellationTokenSource? WithShutdownToken<TRequest, TResponse>(
            ref ClientInterceptorContext<TRequest, TResponse> context)
            where TRequest : class
            where TResponse : class
        {
            CallOptions options = context.Options;
            CancellationToken own = options.CancellationToken;
            CancellationTokenSource? linked = null;
            CancellationToken token;
            if (own.CanBeCanceled)
            {
                linked = CancellationTokenSource.CreateLinkedTokenSource(own, _shutdown);
                token = linked.Token;
            }
            else
            {
                token = _shutdown;
            }

            context = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method,
                context.Host,
                options.WithCancellationToken(token));
            return linked;
        }
    }
}
