using System;
using Microsoft.Xna.Framework;

namespace WebAnnotation.UI
{
    /// <summary>Which flag toggle a <see cref="FlagFeedbackAnimation"/> celebrates or mourns.</summary>
    internal enum FlagFeedbackKind
    {
        /// <summary>Terminal was just turned on: confetti bursts outward from the tip.</summary>
        Celebration,

        /// <summary>Off Edge was just turned on: a frown face slides down and fades.</summary>
        Frown,
    }

    /// <summary>
    /// The pure, time-based math for the short feedback animation shown when a location is marked Terminal
    /// or Off Edge. It owns no graphics resources and makes no server or tile calls; <see cref="FlagFeedbackRenderer"/>
    /// draws it. Everything is a function of the elapsed seconds so it can be unit tested without a GraphicsDevice.
    /// </summary>
    /// <remarks>
    /// Callers: <see cref="FlagFeedbackRenderer.Start"/> creates one per toggle. The animation is anchored at a
    /// world position by the renderer, so it follows the annotation while the user pans; all offsets here are in
    /// screen pixels relative to that anchor. Instances are immutable and safe to read from the draw thread.
    /// </remarks>
    internal sealed class FlagFeedbackAnimation
    {
        /// <summary>Length of the confetti burst in seconds.</summary>
        public const double CelebrationSeconds = 2.0;

        /// <summary>Length of the frown slide in seconds.</summary>
        public const double FrownSeconds = 1.5;

        /// <summary>Number of confetti pieces in a burst.</summary>
        public const int ParticleCount = 40;

        /// <summary>Downward pull on confetti in pixels per second squared.</summary>
        public const float Gravity = 160f;

        private const float MinSpeed = 70f;
        private const float MaxSpeed = 190f;

        private readonly float[] angles = new float[ParticleCount];
        private readonly float[] speeds = new float[ParticleCount];

        /// <summary>Creates an animation of the given kind. The seed fixes the particle pattern so tests are repeatable.</summary>
        public FlagFeedbackAnimation(FlagFeedbackKind kind, int seed)
        {
            Kind = kind;
            var random = new Random(seed);
            for (int i = 0; i < ParticleCount; i++)
            {
                // Evenly spaced directions with jitter keep the burst round instead of clumped.
                angles[i] = (float)((i + random.NextDouble() * 0.8) / ParticleCount * 2.0 * Math.PI);
                speeds[i] = MinSpeed + (float)random.NextDouble() * (MaxSpeed - MinSpeed);
            }
        }

        /// <summary>Which toggle this animation is for.</summary>
        public FlagFeedbackKind Kind { get; }

        /// <summary>Total running time in seconds for this kind.</summary>
        public double DurationSeconds => Kind == FlagFeedbackKind.Celebration ? CelebrationSeconds : FrownSeconds;

        /// <summary>True once <paramref name="elapsedSeconds"/> has reached <see cref="DurationSeconds"/>; the renderer then drops it.</summary>
        public bool IsFinished(double elapsedSeconds) => elapsedSeconds >= DurationSeconds;

        /// <summary>Elapsed time as a 0 to 1 fraction, clamped. Negative elapsed (clock hiccup) reads as 0.</summary>
        public double Progress(double elapsedSeconds) =>
            Math.Max(0.0, Math.Min(1.0, elapsedSeconds / DurationSeconds));

        /// <summary>
        /// Opacity from 0 to 1. Confetti is solid for the first 40 percent then fades to nothing; the frown fades
        /// linearly over its whole slide so it is gone exactly when it has travelled one face length.
        /// </summary>
        public float Alpha(double elapsedSeconds)
        {
            double p = Progress(elapsedSeconds);
            if (Kind == FlagFeedbackKind.Frown)
                return (float)(1.0 - p);

            const double holdFraction = 0.4;
            return p <= holdFraction ? 1f : (float)(1.0 - (p - holdFraction) / (1.0 - holdFraction));
        }

        /// <summary>Distance the frown has slid down in pixels: linear from 0 to exactly <paramref name="faceSizePixels"/>.</summary>
        public float FrownSlideOffset(double elapsedSeconds, float faceSizePixels) =>
            (float)Progress(elapsedSeconds) * faceSizePixels;

        /// <summary>
        /// Position of confetti piece <paramref name="index"/> relative to the burst origin, in screen pixels
        /// (y grows downward). Moves outward at its own speed while gravity bends it down.
        /// </summary>
        public Vector2 ParticleOffset(int index, double elapsedSeconds)
        {
            if (index < 0 || index >= ParticleCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            float t = (float)Math.Max(0.0, Math.Min(elapsedSeconds, DurationSeconds));
            float x = (float)Math.Cos(angles[index]) * speeds[index] * t;
            float y = (float)Math.Sin(angles[index]) * speeds[index] * t + 0.5f * Gravity * t * t;
            return new Vector2(x, y);
        }
    }
}
