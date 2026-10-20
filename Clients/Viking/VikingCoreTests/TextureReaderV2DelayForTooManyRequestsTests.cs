using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="TextureReaderV2"/> HTTP 429 retry delay: Retry-After delta seconds, HTTP-date (past and future),
    /// missing header jitter via <see cref="Geometry.Global.GetRandomRequestDelay"/>, negative delay clamped to zero,
    /// and delays above 60 s capped.
    /// </summary>
    /// <remarks>
    /// <see cref="TextureReaderV2ResponseDisposalTests"/> covers end-to-end 429 retries; this class targets the private
    /// <c>DelayForTooManyRequests</c> helper only, invoked through reflection.
    /// </remarks>
    [TestClass]
    public class TextureReaderV2DelayForTooManyRequestsTests
    {
        private static readonly MethodInfo DelayForTooManyRequests =
            typeof(TextureReaderV2).GetMethod("DelayForTooManyRequests", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(TextureReaderV2), "DelayForTooManyRequests");

        private static TimeSpan InvokeDelay(HttpResponseMessage response) =>
            (TimeSpan)DelayForTooManyRequests.Invoke(null, [response])!;

        private static HttpResponseMessage Response429() => new((HttpStatusCode)429);

        [TestMethod]
        public void RetryAfterDeltaSecondsUsedAsDelay()
        {
            using var response = Response429();
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12.5));

            TimeSpan delay = InvokeDelay(response);

            Assert.AreEqual(TimeSpan.FromSeconds(12.5), delay);
        }

        [TestMethod]
        public void RetryAfterHttpDateInFutureUsesRemainingTime()
        {
            using var response = Response429();
            DateTimeOffset target = DateTimeOffset.UtcNow.AddSeconds(25);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(target);

            TimeSpan delay = InvokeDelay(response);

            Assert.IsTrue(delay.TotalSeconds >= 24 && delay.TotalSeconds <= 26,
                $"Expected ~25 s, got {delay.TotalSeconds:F3} s");
        }

        [TestMethod]
        public void RetryAfterHttpDateInPastClampsToZero()
        {
            using var response = Response429();
            response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-2));

            Assert.AreEqual(TimeSpan.Zero, InvokeDelay(response));
        }

        [TestMethod]
        public void NegativeDeltaClampsToZero()
        {
            using var response = Response429();
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(-3));

            Assert.AreEqual(TimeSpan.Zero, InvokeDelay(response));
        }

        [TestMethod]
        public void DelayAboveSixtySecondsCapsAtSixtySeconds()
        {
            using var response = Response429();
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));

            Assert.AreEqual(TimeSpan.FromMilliseconds(60_000), InvokeDelay(response));
        }

        [TestMethod]
        public void DelayExactlySixtySecondsIsNotCapped()
        {
            using var response = Response429();
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));

            Assert.AreEqual(TimeSpan.FromSeconds(60), InvokeDelay(response));
        }

        [TestMethod]
        public void MissingRetryAfterUsesRandomRequestDelayRange()
        {
            for (int i = 0; i < 40; i++)
            {
                using var response = Response429();
                TimeSpan delay = InvokeDelay(response);
                double ms = delay.TotalMilliseconds;
                Assert.IsTrue(ms >= 800 && ms < 1200, $"Sample {i}: {ms} ms outside [800, 1200)");
            }
        }

        [TestMethod]
        public void MissingRetryAfterMatchesGetRandomRequestDelayBounds()
        {
            Prop.ForAll(
                Arb.Default.PositiveInt(),
                _ =>
                {
                    using var response = Response429();
                    double ms = InvokeDelay(response).TotalMilliseconds;
                    return ms >= 800 && ms < 1200;
                }).QuickCheckThrowOnFailure();
        }
    }
}
