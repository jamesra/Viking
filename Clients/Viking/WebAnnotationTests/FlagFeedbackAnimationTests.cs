using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using WebAnnotation.UI;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the pure timing, alpha, slide and particle math of <see cref="FlagFeedbackAnimation"/> and the shape of the
    /// generated frown face, so the Terminal confetti and Off Edge frown stay inside the 1.5 to 2 second window and
    /// end fully faded without needing a GraphicsDevice.
    /// </summary>
    [TestClass]
    public class FlagFeedbackAnimationTests
    {
        [TestMethod]
        public void Durations_AreWithinOneAndAHalfToTwoSeconds()
        {
            Assert.AreEqual(2.0, new FlagFeedbackAnimation(FlagFeedbackKind.Celebration, 1).DurationSeconds, 1e-9);
            double frown = new FlagFeedbackAnimation(FlagFeedbackKind.Frown, 1).DurationSeconds;
            Assert.IsTrue(frown >= 1.5 && frown <= 2.0);
        }

        [TestMethod]
        public void IsFinished_FalseBeforeAndTrueAtDuration()
        {
            var a = new FlagFeedbackAnimation(FlagFeedbackKind.Frown, 1);
            Assert.IsFalse(a.IsFinished(0));
            Assert.IsFalse(a.IsFinished(a.DurationSeconds - 0.01));
            Assert.IsTrue(a.IsFinished(a.DurationSeconds));
        }

        [TestMethod]
        public void Progress_IsClamped()
        {
            var a = new FlagFeedbackAnimation(FlagFeedbackKind.Celebration, 1);
            Assert.AreEqual(0.0, a.Progress(-1.0), 1e-9);
            Assert.AreEqual(0.5, a.Progress(1.0), 1e-9);
            Assert.AreEqual(1.0, a.Progress(10.0), 1e-9);
        }

        [TestMethod]
        public void Frown_AlphaFadesLinearlyToZero()
        {
            var a = new FlagFeedbackAnimation(FlagFeedbackKind.Frown, 1);
            Assert.AreEqual(1f, a.Alpha(0), 1e-6);
            Assert.AreEqual(0.5f, a.Alpha(a.DurationSeconds / 2), 1e-6);
            Assert.AreEqual(0f, a.Alpha(a.DurationSeconds), 1e-6);
        }

        [TestMethod]
        public void Frown_SlidesExactlyOneFaceLength()
        {
            var a = new FlagFeedbackAnimation(FlagFeedbackKind.Frown, 1);
            Assert.AreEqual(0f, a.FrownSlideOffset(0, 44f), 1e-4);
            Assert.AreEqual(22f, a.FrownSlideOffset(a.DurationSeconds / 2, 44f), 1e-4);
            Assert.AreEqual(44f, a.FrownSlideOffset(a.DurationSeconds, 44f), 1e-4);
            Assert.AreEqual(44f, a.FrownSlideOffset(a.DurationSeconds * 3, 44f), 1e-4);
        }

        [TestMethod]
        public void Celebration_AlphaHoldsThenFadesToZero()
        {
            var a = new FlagFeedbackAnimation(FlagFeedbackKind.Celebration, 1);
            Assert.AreEqual(1f, a.Alpha(0.5), 1e-6);
            Assert.AreEqual(1f, a.Alpha(0.4 * a.DurationSeconds), 1e-6);
            Assert.AreEqual(0.5f, a.Alpha(0.7 * a.DurationSeconds), 1e-5);
            Assert.AreEqual(0f, a.Alpha(a.DurationSeconds), 1e-6);
        }

        [TestMethod]
        public void Particles_StartAtOriginAndBurstOutward()
        {
            var a = new FlagFeedbackAnimation(FlagFeedbackKind.Celebration, 7);
            float maxRadius = 0f;
            for (int i = 0; i < FlagFeedbackAnimation.ParticleCount; i++)
            {
                Assert.AreEqual(Vector2.Zero, a.ParticleOffset(i, 0));
                Vector2 early = a.ParticleOffset(i, 0.2);
                Vector2 later = a.ParticleOffset(i, 0.6);
                Assert.IsTrue(later.Length() > early.Length(), "piece " + i + " should move away from the tip");
                maxRadius = Math.Max(maxRadius, later.Length());
            }

            Assert.IsTrue(maxRadius > 40f);
        }

        [TestMethod]
        public void Particles_SpreadInAllDirections()
        {
            var a = new FlagFeedbackAnimation(FlagFeedbackKind.Celebration, 3);
            bool left = false, right = false, up = false;
            for (int i = 0; i < FlagFeedbackAnimation.ParticleCount; i++)
            {
                Vector2 p = a.ParticleOffset(i, 0.3);
                left |= p.X < -5f;
                right |= p.X > 5f;
                up |= p.Y < -5f;
            }

            Assert.IsTrue(left && right && up);
        }

        [TestMethod]
        public void Particles_SameSeedGivesSamePattern()
        {
            var a = new FlagFeedbackAnimation(FlagFeedbackKind.Celebration, 42);
            var b = new FlagFeedbackAnimation(FlagFeedbackKind.Celebration, 42);
            Assert.AreEqual(a.ParticleOffset(5, 1.0), b.ParticleOffset(5, 1.0));
        }

        [TestMethod]
        public void ParticleOffset_RejectsBadIndex()
        {
            var a = new FlagFeedbackAnimation(FlagFeedbackKind.Celebration, 1);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.ParticleOffset(FlagFeedbackAnimation.ParticleCount, 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => a.ParticleOffset(-1, 0));
        }

        [TestMethod]
        public void FacePixels_OpaqueDiscWithTransparentCornersAndDarkFeatures()
        {
            const int size = 64;
            Color[] px = FlagFeedbackRenderer.BuildFacePixels(size);
            Assert.AreEqual(size * size, px.Length);
            Assert.AreEqual(0, px[0].A);
            Assert.AreEqual(255, px[size / 2 * size + size / 2].A);

            Color skin = px[(int)(0.55 * size) * size + (int)(0.5 * size)];
            Color eye = px[(int)(0.38 * size) * size + (int)(0.35 * size)];
            Color mouth = px[(int)(0.63 * size) * size + (int)(0.5 * size)];
            Assert.IsTrue(eye.R < skin.R / 2, "eye should be darker than the face");
            Assert.IsTrue(mouth.R < skin.R / 2, "mouth arc top should be dark");
        }

        [TestMethod]
        public void FacePixels_MouthEndsPointDownNotUp()
        {
            const int size = 64;
            Color[] px = FlagFeedbackRenderer.BuildFacePixels(size);
            Color center = px[(int)(0.63 * size) * size + (int)(0.5 * size)];
            Color endLow = px[(int)(0.74 * size) * size + (int)(0.29 * size)];
            Color aboveEnd = px[(int)(0.66 * size) * size + (int)(0.29 * size)];
            Assert.IsTrue(center.R < 100, "arc top is at the middle");
            Assert.IsTrue(endLow.R < aboveEnd.R, "arc ends sit lower than the middle, so it frowns");
        }
    }
}
