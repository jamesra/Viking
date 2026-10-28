using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;

namespace VikingXNAGraphics
{
    /// <summary>
    /// The shader that draws labels cached as a <see cref="LabelOutlineMap"/>: text, a border of any width with an adjustable blur, and an
    /// optional shadow, coloured at draw time. Begin a <see cref="SpriteBatch"/> with <see cref="Effect"/>, call <see cref="Apply"/>, then
    /// draw labels with <see cref="LabelView.DrawWithOutlineMap"/>. <see cref="Effect"/> is null when the shader could not be loaded, and callers
    /// then draw labels the plain way.
    /// </summary>
    public class LabelOutlineEffect : IInitEffect
    {
        /// <summary>Screen pixels from a glyph's edge to the middle of the border's fade.</summary>
        public static float OutlineWidth { get; set; } = 1.5f;

        /// <summary>Screen pixels the border takes to fade out; 1 is a crisp edge, larger blurs it. On by default.</summary>
        public static float OutlineBlur { get; set; } = 2f;

        /// <summary>How opaque the shadow is, 0 to 1. Off (0) by default.</summary>
        public static float ShadowOpacity { get; set; } = 0f;

        /// <summary>Screen pixels the shadow falls to the right and down.</summary>
        public static Vector2 ShadowOffset { get; set; } = new(1.5f, 1.5f);

        /// <summary>Screen pixels the shadow takes to fade out.</summary>
        public static float ShadowBlur { get; set; } = 3f;

        /// <summary>The loaded effect, or null when it is not available.</summary>
        public Effect Effect { get; private set; }

        private EffectParameter outlineColor;
        private EffectParameter outlineWidth;
        private EffectParameter outlineBlur;
        private EffectParameter shadowColor;
        private EffectParameter shadowOpacity;
        private EffectParameter shadowOffset;
        private EffectParameter shadowBlur;

        public void Init(GraphicsDevice device, ContentManager content)
        {
            try
            {
                Effect = content.Load<Effect>("LabelOutline");
                outlineColor = Effect.Parameters["OutlineColor"];
                outlineWidth = Effect.Parameters["OutlineWidth"];
                outlineBlur = Effect.Parameters["OutlineBlur"];
                shadowColor = Effect.Parameters["ShadowColor"];
                shadowOpacity = Effect.Parameters["ShadowOpacity"];
                shadowOffset = Effect.Parameters["ShadowOffset"];
                shadowBlur = Effect.Parameters["ShadowBlur"];
            }
            catch (Exception ex) when (ex is ContentLoadException or InvalidOperationException or NullReferenceException)
            {
                // A build without the compiled effect still draws labels, with the plain outline.
                Trace.WriteLine($"Label outline shader unavailable: {ex.Message}");
                Effect = null;
            }
        }

        /// <summary>What a batch draws.</summary>
        public enum Layer
        {
            /// <summary>Shadow, border and text of each label.</summary>
            All,

            /// <summary>Only the shadow and border. Draw this batch for a run of neighbouring labels, then <see cref="TextOnly"/>, so no border covers a neighbour's text.</summary>
            BorderOnly,

            /// <summary>Only the text.</summary>
            TextOnly,
        }

        /// <summary>
        /// Sets the shader's colors and the static settings for the next batch. <paramref name="outline"/> is the border's color and the
        /// shadow's; the text takes the color each label is drawn in. Call it before <c>SpriteBatch.Begin</c>.
        /// </summary>
        public void Apply(Color outline, Layer layer = Layer.All)
        {
            if (Effect is null)
                return;

            Effect.CurrentTechnique = Effect.Techniques[layer switch
            {
                Layer.BorderOnly => "LabelOutlineBorderOnly",
                Layer.TextOnly => "LabelOutlineTextOnly",
                _ => "LabelOutlineDrawing",
            }];

            Vector3 rgb = outline.ToVector3();
            outlineColor.SetValue(rgb);
            shadowColor.SetValue(rgb);
            outlineWidth.SetValue(OutlineWidth);
            outlineBlur.SetValue(OutlineBlur);
            shadowOpacity.SetValue(ShadowOpacity);
            shadowOffset.SetValue(ShadowOffset);
            shadowBlur.SetValue(ShadowBlur);
        }

        /// <summary>The effect stays with the ContentManager that loaded it.</summary>
        public void Dispose()
        {
        }
    }
}
