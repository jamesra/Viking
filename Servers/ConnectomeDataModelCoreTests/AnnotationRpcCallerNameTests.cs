using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins <c>AnnotationRpc.CallerName</c>, the username every annotation write is stamped with.
    /// Lookup order: <c>Identity.Name</c>, then the <c>preferred_username</c> claim, then <c>sub</c>,
    /// then <see cref="ClaimTypes.NameIdentifier"/>; a null or whitespace value falls through to the
    /// next source, and no user claims at all yield "unknown".
    /// </summary>
    public class AnnotationRpcCallerNameTests
    {
        /// <summary>Authentication type; a ClaimsIdentity without one is unauthenticated.</summary>
        private const string AuthType = "test";

        /// <summary>Identity.Name wins over every other source.</summary>
        [Fact]
        public void IdentityNameWinsOverOtherClaims()
        {
            var context = ContextFor(Principal(
                new Claim(ClaimTypes.Name, "name-user"),
                new Claim("preferred_username", "preferred-user"),
                new Claim("sub", "sub-user"),
                new Claim(ClaimTypes.NameIdentifier, "nameid-user")));

            Assert.Equal("name-user", AnnotationRpc.CallerName(context));
        }

        /// <summary>With no name claim, preferred_username beats sub and NameIdentifier.</summary>
        [Fact]
        public void PreferredUsernameWinsOverSubAndNameIdentifier()
        {
            var context = ContextFor(Principal(
                new Claim("preferred_username", "preferred-user"),
                new Claim("sub", "sub-user"),
                new Claim(ClaimTypes.NameIdentifier, "nameid-user")));

            Assert.Equal("preferred-user", AnnotationRpc.CallerName(context));
        }

        /// <summary>sub beats NameIdentifier when both are present.</summary>
        [Fact]
        public void SubWinsOverNameIdentifier()
        {
            var context = ContextFor(Principal(
                new Claim("sub", "sub-user"),
                new Claim(ClaimTypes.NameIdentifier, "nameid-user")));

            Assert.Equal("sub-user", AnnotationRpc.CallerName(context));
        }

        /// <summary>NameIdentifier is the last source consulted.</summary>
        [Fact]
        public void NameIdentifierUsedWhenNothingElseIsPresent()
        {
            var context = ContextFor(Principal(new Claim(ClaimTypes.NameIdentifier, "nameid-user")));

            Assert.Equal("nameid-user", AnnotationRpc.CallerName(context));
        }

        /// <summary>A whitespace Identity.Name falls through to preferred_username.</summary>
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t \r\n")]
        public void BlankIdentityNameFallsThroughToPreferredUsername(string blank)
        {
            var context = ContextFor(Principal(
                new Claim(ClaimTypes.Name, blank),
                new Claim("preferred_username", "preferred-user")));

            Assert.Equal("preferred-user", AnnotationRpc.CallerName(context));
        }

        /// <summary>A whitespace preferred_username falls through to sub.</summary>
        [Fact]
        public void BlankPreferredUsernameFallsThroughToSub()
        {
            var context = ContextFor(Principal(
                new Claim("preferred_username", "  "),
                new Claim("sub", "sub-user")));

            Assert.Equal("sub-user", AnnotationRpc.CallerName(context));
        }

        /// <summary>A whitespace sub does not fall back to NameIdentifier: only a missing sub does.</summary>
        [Fact]
        public void BlankSubIsNotReplacedByNameIdentifier()
        {
            var context = ContextFor(Principal(
                new Claim("sub", " "),
                new Claim(ClaimTypes.NameIdentifier, "nameid-user")));

            Assert.Equal("unknown", AnnotationRpc.CallerName(context));
        }

        /// <summary>Every source blank or absent yields "unknown".</summary>
        [Fact]
        public void AllBlankSourcesGiveUnknown()
        {
            var context = ContextFor(Principal(
                new Claim(ClaimTypes.Name, " "),
                new Claim("preferred_username", ""),
                new Claim("sub", "\t")));

            Assert.Equal("unknown", AnnotationRpc.CallerName(context));
        }

        /// <summary>An anonymous principal (no identity claims) yields "unknown".</summary>
        [Fact]
        public void AnonymousUserGivesUnknown()
        {
            Assert.Equal("unknown", AnnotationRpc.CallerName(ContextFor(new ClaimsPrincipal())));
        }

        /// <summary>The default DefaultHttpContext user (an unauthenticated identity) yields "unknown".</summary>
        [Fact]
        public void DefaultHttpContextUserGivesUnknown()
        {
            Assert.Equal("unknown", AnnotationRpc.CallerName(new TestCallContext(new DefaultHttpContext())));
        }

        /// <summary>
        /// A call context not hosted by ASP.NET Core throws: Grpc.AspNetCore's GetHttpContext does not return null,
        /// so the null-conditional access in CallerName never sees a missing HTTP context.
        /// </summary>
        [Fact]
        public void MissingHttpContextThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(() => AnnotationRpc.CallerName(new TestCallContext(null)));
        }

        /// <summary>The returned name is the claim value verbatim: no trimming.</summary>
        [Fact]
        public void ReturnedNameIsNotTrimmed()
        {
            var context = ContextFor(Principal(new Claim(ClaimTypes.Name, " padded ")));

            Assert.Equal(" padded ", AnnotationRpc.CallerName(context));
        }

        private static ClaimsPrincipal Principal(params Claim[] claims) =>
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthType));

        private static ServerCallContext ContextFor(ClaimsPrincipal user) =>
            new TestCallContext(new DefaultHttpContext { User = user });

        /// <summary>Call context carrying the given HTTP context (as ASP.NET Core hosting supplies), or none when null.</summary>
        private sealed class TestCallContext : ServerCallContext
        {
            /// <summary>Key Grpc.AspNetCore's GetHttpContext reads from <see cref="ServerCallContext.UserState"/>.</summary>
            private const string HttpContextKey = "__HttpContext";

            public TestCallContext(HttpContext httpContext)
            {
                if (httpContext != null)
                    UserState[HttpContextKey] = httpContext;
            }

            protected override string MethodCore => "test";
            protected override string HostCore => "localhost";
            protected override string PeerCore => "127.0.0.1";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore { get; } = new();
            protected override CancellationToken CancellationTokenCore => CancellationToken.None;
            protected override Metadata ResponseTrailersCore { get; } = new();
            protected override Status StatusCore { get; set; }
            protected override WriteOptions WriteOptionsCore { get; set; }
            protected override AuthContext AuthContextCore => null;

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) =>
                throw new NotSupportedException();

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        }
    }
}
