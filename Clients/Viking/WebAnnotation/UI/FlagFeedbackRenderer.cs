using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Draws the short Terminal and Off Edge feedback animations (<see cref="FlagFeedbackAnimation"/>) with a
    /// SpriteBatch in screen space, using two tiny textures it generates itself. No fonts, packages, tiles or
    /// server calls are involved.
    /// </summary>
    /// <remarks>
    /// Owned by one <see cref="AnnotationOverlay"/> and used only on the render thread: <see cref="Start"/> is called
    /// from the context-menu click (UI thread, the same thread that renders in WinForms) and <see cref="Draw"/> from the
    /// overlay draw. The textures are created on the first frame and disposed as soon as the last animation ends or
    /// <see cref="Dispose"/> is called, so an idle overlay holds no GPU resources. Animations are anchored at a world
    /// position, so they follow the annotation while the user pans or zooms.
    /// </remarks>
    internal sealed class FlagFeedbackRenderer : IDisposable
    {
        /// <summary>Side length in pixels of the frown face; it slides down by exactly this much.</summary>
        public const float FaceSizePixels = 44f;

        private const int FaceTextureSize = 64;
        private const float ConfettiPieceSize = 5f;

        private static readonly Color[] ConfettiColors =
        [
            new Color(255, 82, 82), new Color(255, 193, 7), new Color(76, 175, 80),
            new Color(33, 150, 243), new Color(171, 71, 188), new Color(255, 255, 255),
        ];

        private sealed class Entry(FlagFeedbackAnimation animation, Geometry.Vector2 worldAnchor)
        {
            public readonly FlagFeedbackAnimation Animation = animation;
            public readonly Geometry.Vector2 WorldAnchor = worldAnchor;
            public readonly Stopwatch Clock = Stopwatch.StartNew();
        }

        private readonly List<Entry> active = [];
        private readonly Random seeds = new();
        private Texture2D? pixel;
        private Texture2D? face;

        /// <summary>True while any animation still needs frames; the overlay keeps invalidating the view until this is false.</summary>
        public bool IsActive => active.Count > 0;

        /// <summary>Begins an animation anchored at <paramref name="worldAnchor"/> (volume coordinates). The caller asks the view to redraw.</summary>
        public void Start(FlagFeedbackKind kind, Geometry.Vector2 worldAnchor) =>
            active.Add(new Entry(new FlagFeedbackAnimation(kind, seeds.Next()), worldAnchor));

        /// <summary>
        /// Draws every running animation and drops the finished ones.
        /// </summary>
        /// <returns>True when another frame is needed (same contract as the auto-polygonize shimmer).</returns>
        public bool Draw(SpriteBatch spriteBatch, VikingXNA.Scene scene)
        {
            if (active.Count == 0)
                return false;

            GraphicsDevice device = spriteBatch.GraphicsDevice;
            if (device.IsDisposed)
            {
                DisposeTextures();
                active.Clear();
                return false;
            }

            EnsureTextures(device);

            DeviceStateManager.SaveDeviceState(device);
            bool batchBegun = false;
            try
            {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                batchBegun = true;

                foreach (Entry entry in active)
                {
                    double elapsed = entry.Clock.Elapsed.TotalSeconds;
                    if (entry.Animation.IsFinished(elapsed))
                        continue;

                    Geometry.Vector2 screen = scene.WorldToScreen(entry.WorldAnchor);
                    var origin = new Vector2((float)screen.X, (float)screen.Y);
                    if (entry.Animation.Kind == FlagFeedbackKind.Celebration)
                        DrawConfetti(spriteBatch, entry.Animation, origin, elapsed);
                    else
                        DrawFrown(spriteBatch, entry.Animation, origin, elapsed);
                }
            }
            finally
            {
                if (batchBegun)
                    spriteBatch.End();
                DeviceStateManager.RestoreDeviceState(device);
            }

            active.RemoveAll(e => e.Animation.IsFinished(e.Clock.Elapsed.TotalSeconds));
            if (active.Count == 0)
                DisposeTextures();

            return active.Count > 0;
        }

        /// <summary>Releases the textures and forgets running animations.</summary>
        public void Dispose()
        {
            active.Clear();
            DisposeTextures();
        }

        private void DrawConfetti(SpriteBatch spriteBatch, FlagFeedbackAnimation animation, Vector2 origin, double elapsed)
        {
            float alpha = animation.Alpha(elapsed);
            var half = new Vector2(0.5f);
            for (int i = 0; i < FlagFeedbackAnimation.ParticleCount; i++)
            {
                Vector2 position = origin + animation.ParticleOffset(i, elapsed);
                // Color * alpha scales all channels, which is correct for the premultiplied AlphaBlend state.
                Color tint = ConfettiColors[i % ConfettiColors.Length] * alpha;
                float spin = i + (float)elapsed * 6f;
                spriteBatch.Draw(pixel, position, null, tint, spin, half, ConfettiPieceSize, SpriteEffects.None, 0f);
            }
        }

        private void DrawFrown(SpriteBatch spriteBatch, FlagFeedbackAnimation animation, Vector2 origin, double elapsed)
        {
            float alpha = animation.Alpha(elapsed);
            float slide = animation.FrownSlideOffset(elapsed, FaceSizePixels);
            var center = new Vector2(origin.X, origin.Y - FaceSizePixels + slide);
            float scale = FaceSizePixels / FaceTextureSize;
            var half = new Vector2(FaceTextureSize / 2f);
            spriteBatch.Draw(face, center, null, Color.White * alpha, 0f, half, scale, SpriteEffects.None, 0f);
        }

        private void EnsureTextures(GraphicsDevice device)
        {
            if (pixel is null || pixel.IsDisposed)
            {
                pixel = new Texture2D(device, 1, 1);
                pixel.SetData([Color.White]);
            }

            if (face is null || face.IsDisposed)
            {
                face = new Texture2D(device, FaceTextureSize, FaceTextureSize);
                face.SetData(BuildFacePixels(FaceTextureSize));
            }
        }

        private void DisposeTextures()
        {
            pixel?.Dispose();
            pixel = null;
            face?.Dispose();
            face = null;
        }

        /// <summary>
        /// Builds the frowning face as premultiplied-alpha pixels: a yellow disc with a dark rim, two eyes and a
        /// downturned mouth, antialiased by distance. Pure so tests can check the shape without a GraphicsDevice.
        /// </summary>
        /// <param name="size">Width and height of the square texture in pixels.</param>
        internal static Color[] BuildFacePixels(int size)
        {
            var pixels = new Color[size * size];
            float rimColorScale = 0.25f;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float u = (px + 0.5f) / size;
                    float v = (py + 0.5f) / size;
                    float fromCenter = (float)Math.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * size;
                    float coverage = Clamp01(0.5f * size - fromCenter);
                    if (coverage <= 0f)
                        continue;

                    float rim = Clamp01((fromCenter - (0.5f * size - 2.5f)) / 1.5f);
                    var baseColor = Vector3.Lerp(new Vector3(1f, 0.82f, 0.2f), new Vector3(1f, 0.82f, 0.2f) * rimColorScale, rim);

                    float eyeLeft = Distance(u, v, 0.35f, 0.38f) * size;
                    float eyeRight = Distance(u, v, 0.65f, 0.38f) * size;
                    float eye = Clamp01(0.085f * size - Math.Min(eyeLeft, eyeRight));

                    // The mouth is the top arc of a circle centered below the face, so its ends point down: a frown.
                    float mouthRadius = Distance(u, v, 0.5f, 0.88f) * size;
                    bool aboveCenter = v < 0.76f;
                    float mouth = aboveCenter ? Clamp01(0.04f * size - Math.Abs(mouthRadius - 0.25f * size)) : 0f;

                    float dark = Math.Max(eye, mouth);
                    Vector3 rgb = Vector3.Lerp(baseColor, new Vector3(0.12f, 0.08f, 0.04f), dark);
                    pixels[py * size + px] = new Color(rgb.X * coverage, rgb.Y * coverage, rgb.Z * coverage, coverage);
                }
            }

            return pixels;
        }

        private static float Distance(float u, float v, float cx, float cy) =>
            (float)Math.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
