using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace VikingXNAGraphics
{
    public class OverlayShaderEffect : IInitEffect
    {
        public enum Techniques
        {
            /// <summary>
            /// Color defined by effect. 
            /// Normal alpha blending
            /// </summary>
            SingleColorAlphaOverlayEffect,
            /// <summary>
            /// Color defined by vertex. 
            /// Normal alpha blending
            /// </summary>
            VertexColorAlphaOverlayEffect,
            /// <summary>
            /// Color defined by RGBA Texture
            /// Normal alpha blending
            /// </summary>
            TextureAlphaOverlayEffect,
            /// <summary>
            /// Color defined by effect
            /// Level defined by grayscale texture
            /// Alpha defined by texture
            /// </summary>
            SingleColorTextureAlphaOverlayEffect,
            /// <summary>
            /// Color defined by vertex
            /// Level defined by grayscale texture (Color * texture)
            /// Alpha defined by texture
            /// </summary>
            VertexColorTextureAlphaOverlayEffect,

            /// <summary>
            /// Color defined by effect. 
            /// Normal alpha blending
            /// Output limited to unit circle
            /// </summary>
            CircleSingleColorAlphaOverlayEffect,
            /// <summary>
            /// Color defined by vertex. 
            /// Normal alpha blending
            /// Output limited to unit circle
            /// </summary>
            CircleVertexColorAlphaOverlayEffect,
            /// <summary>
            /// Color defined by RGBA Texture
            /// Normal alpha blending
            /// Output limited to unit circle
            /// </summary>
            CircleTextureAlphaOverlayEffect,
            /// <summary>
            /// Color defined by effect
            /// Level defined by grayscale texture
            /// Alpha defined by texture
            /// Output limited to unit circle
            /// </summary>
            CircleSingleColorTextureAlphaOverlayEffect,
            /// <summary>
            /// Color defined by vertex
            /// Level defined by grayscale texture (Color * texture)
            /// Alpha defined by texture
            /// Output limited to unit circle
            /// </summary>
            CircleVertexColorTextureAlphaOverlayEffect,

            /// <summary>
            /// Color defined by effect. 
            /// Background luma determines blending
            /// </summary>
            SingleColorLumaOverlayEffect,
            /// <summary>
            /// Color defined by vertex. 
            /// Background luma determines blending
            /// </summary>
            VertexColorLumaOverlayEffect,
            /// <summary>
            /// Color defined by RGBA Texture
            /// Background luma determines blending
            /// </summary>
            TextureLumaOverlayEffect,
            /// <summary>
            /// Color defined by effect
            /// Level defined by grayscale texture
            /// Background luma determines blending
            /// Alpha defined by texture
            /// </summary>
            SingleColorTextureLumaOverlayEffect,
            /// <summary>
            /// Color defined by vertex
            /// Level defined by grayscale texture (Color * texture)
            /// Background luma determines blending
            /// Alpha defined by texture
            /// </summary>
            VertexColorTextureLumaOverlayEffect,


            /// <summary>
            /// Color defined by effect. 
            /// Background luma determines blending
            /// Output limited to unit circle
            /// </summary>
            CircleSingleColorLumaOverlayEffect,
            /// <summary>
            /// Color defined by vertex. 
            /// Background luma determines blending
            /// Output limited to unit circle
            /// </summary>
            CircleVertexColorLumaOverlayEffect,
            /// <summary>
            /// Color defined by RGBA Texture
            /// Background luma determines blending
            /// Output limited to unit circle
            /// </summary>
            CircleTextureLumaOverlayEffect,
            /// <summary>
            /// Color defined by effect
            /// Level defined by grayscale texture
            /// Background luma determines blending
            /// Alpha defined by texture
            /// Output limited to unit circle
            /// </summary>
            CircleSingleColorTextureLumaOverlayEffect,
            /// <summary>
            /// Color defined by vertex
            /// Level defined by grayscale texture (Color * texture)
            /// Background luma determines blending
            /// Alpha defined by texture
            /// Output limited to unit circle
            /// </summary>
            CircleVertexColorTextureLumaOverlayEffect,

            /// <summary>
            /// Color defined by effect (hue/chroma), luma set relative to the background by <see cref="OverlayShaderEffect.RingContrast"/>.
            /// Procedural ring, no texture: band on the outer radius with a quick fade to clear toward the center.
            /// The band is dark over bright background and light over dark background, so the background texture must be set.
            /// Output limited to unit circle
            /// </summary>
            CircleRingOverlayEffect,

            /// <summary>
            /// Procedural white stripes inside the alpha of <see cref="OverlayShaderEffect.StripeClipTexture"/>, anti-aliased per pixel.
            /// Sizes are in the mask texels given by <see cref="OverlayShaderEffect.StripeTexelCount"/>, so the stripes scale with zoom.
            /// Normal alpha blending
            /// </summary>
            StripeOverlayEffect
        };

        public Effect effect;

        private EffectParameter _WorldViewProjMatrix;
        private EffectParameter _RenderTargetSize;

        private EffectParameter _BackgroundTexture;
        private EffectParameter _AnnotationTexture;

        private EffectParameter _BackgroundSize;

        private EffectParameter _RadiusSquared;
        private EffectParameter _BorderStartRadius;
        private EffectParameter _BorderStartSquared;

        private EffectParameter _BorderBlendStartRadius;
        private EffectParameter _BorderBlendStartSquared;

        private EffectParameter _RingSolidStart;
        private EffectParameter _RingFadeStart;
        private EffectParameter _RingContrast;
        private EffectParameter _RingOpacity;
        private EffectParameter _RingAdaptMid;

        private EffectParameter _StripeClipTexture;
        private EffectParameter _StripeTexelCount;
        private EffectParameter _StripeDirection;
        private EffectParameter _StripePeriod;
        private EffectParameter _StripeBand;
        private EffectParameter _StripeClipLevel;
        private EffectParameter _StripeAlpha;

        private EffectParameter _InputLumaAlpha;

        private EffectParameter _AnnotationHSLColor;

        public Viewport RenderTargetSize
        {
            set => _RenderTargetSize.SetValue(new Vector2(value.Width, value.Height));
        }

        public Matrix WorldViewProjMatrix
        {
            get => _WorldViewProjMatrix.GetValueMatrix();
            set => _WorldViewProjMatrix.SetValue(value);
        }

        public Texture LumaTexture
        {
            set => _BackgroundTexture.SetValue(value);
        }

        public Texture AnnotationTexture
        {
            set => _AnnotationTexture.SetValue(value);
        }

        public Techniques Technique
        {
            set
            {
                string techniqueName = value.ToString();
                effect.CurrentTechnique = effect.Techniques[techniqueName];
                /*switch (value)
                {
                    case Techniques.RGBCircleLumaOverlayEffect:
                        effect.CurrentTechnique = effect.Techniques["RGBCircleLumaOverlayEffect"];
                        break;
                    case Techniques.RGBTextureLumaOverlayEffect:
                        effect.CurrentTechnique = effect.Techniques["RGBTextureLumaOverlayEffect"];
                        break;
                    default:
                        throw new ArgumentException("Unknown technique");
                }*/
            }
        }

        public EffectTechnique CurrentTechnique => this.effect.CurrentTechnique;

        /// <summary>
        /// Radius fraction (0 = center, 1 = rim) where the fully opaque band of <see cref="Techniques.CircleRingOverlayEffect"/>
        /// begins. The band runs from here to the rim.
        /// </summary>
        public float RingSolidStart
        {
            get => _RingSolidStart.GetValueSingle();
            set => _RingSolidStart.SetValue(value);
        }

        /// <summary>
        /// Radius fraction where the ring becomes fully clear. Must be less than <see cref="RingSolidStart"/>;
        /// the fade runs between the two.
        /// </summary>
        public float RingFadeStart
        {
            get => _RingFadeStart.GetValueSingle();
            set => _RingFadeStart.SetValue(value);
        }

        /// <summary>
        /// Average background luma (0..1) above which the ring is darker than the background, otherwise lighter.
        /// </summary>
        public float RingAdaptMid
        {
            get => _RingAdaptMid.GetValueSingle();
            set => _RingAdaptMid.SetValue(value);
        }

        /// <summary>
        /// How far (in luma, 0..1) the ring sits from the average background luma: darker over bright tissue,
        /// lighter over dark tissue.
        /// </summary>
        public float RingContrast
        {
            get => _RingContrast.GetValueSingle();
            set => _RingContrast.SetValue(value);
        }

        /// <summary>
        /// Overall ring opacity (0..1), multiplied with the annotation color's alpha.
        /// </summary>
        public float RingOpacity
        {
            get => _RingOpacity.GetValueSingle();
            set => _RingOpacity.SetValue(value);
        }

        /// <summary>
        /// Texture whose alpha clips <see cref="Techniques.StripeOverlayEffect"/>. Sampled with linear filtering.
        /// </summary>
        public Texture StripeClipTexture
        {
            set => _StripeClipTexture.SetValue(value);
        }

        /// <summary>Mask texels across and down the quad; the other stripe sizes are in these texels.</summary>
        public Vector2 StripeTexelCount
        {
            set => _StripeTexelCount.SetValue(value);
        }

        /// <summary>Stripes run perpendicular to this direction, in texels, e.g. (1, 1) or (1, -1).</summary>
        public Vector2 StripeDirection
        {
            set => _StripeDirection.SetValue(value);
        }

        /// <summary>Distance along <see cref="StripeDirection"/> from one stripe to the next. Stripe and gap are equal.</summary>
        public float StripePeriod
        {
            set => _StripePeriod.SetValue(value);
        }

        /// <summary>
        /// Band across x + y that limits the stripes: X is its centre and Y its half width, in texels.
        /// A half width of zero or less lights every stripe. While a band is set, the stripes drift sideways as X advances.
        /// </summary>
        public Vector2 StripeBand
        {
            set => _StripeBand.SetValue(value);
        }

        /// <summary>Clip texture alpha (0..1) at the edge of the striped area.</summary>
        public float StripeClipLevel
        {
            set => _StripeClipLevel.SetValue(value);
        }

        /// <summary>Alpha of a fully lit stripe.</summary>
        public float StripeAlpha
        {
            set => _StripeAlpha.SetValue(value);
        }

        public float InputLumaAlphaValue
        {
            get => _InputLumaAlpha.GetValueSingle();
            set => _InputLumaAlpha.SetValue(value);
        }

        /// <summary>
        /// The color of the annotation in HSL space
        /// </summary>
        public Color AnnotationColorHSL
        {
            get => _AnnotationHSLColor.GetValueInt32().ToXNAColor();
            set => _AnnotationHSLColor.SetValue(value.ToVector4());
        }

        public void Init(GraphicsDevice device, ContentManager content)
        {
            this.effect = content.Load<Effect>("BillboardAnnotation");
            LoadParameters(this.effect);
            //this.Technique = Techniques.EffectColorLumaOverlayEffect;
        }

        /// <summary>
        /// Content-loaded Effect stays with ContentManager.
        /// </summary>
        public void Dispose()
        {
        }

        /*
        public static OverlayShaderEffect WrapEffect(Effect effect)
        {
            OverlayShaderEffect obj = new OverlayShaderEffect();

            obj.effect = effect;
            obj.LoadParameters(effect);
            obj.Technique = Techniques.RGBTextureLumaOverlayEffect;

            return obj;
        }
        */

        private void LoadParameters(Effect effect)
        {
            _WorldViewProjMatrix = effect.Parameters["mWorldViewProj"];
            _BackgroundTexture = effect.Parameters["BackgroundTexture"];
            _AnnotationTexture = effect.Parameters["AnnotationTexture"];
            _RenderTargetSize = effect.Parameters["RenderTargetSize"];

            _BackgroundSize = effect.Parameters["BackgroundSize"];

            _RadiusSquared = effect.Parameters["radiusSquared"];
            _BorderStartRadius = effect.Parameters["borderStartRadius"];
            _BorderStartSquared = effect.Parameters["borderStartSquared"];
            _BorderBlendStartRadius = effect.Parameters["borderBlendStartRadius"];
            _BorderBlendStartSquared = effect.Parameters["borderBlendStartSquared"];

            _RingSolidStart = effect.Parameters["ringSolidStart"];
            _RingFadeStart = effect.Parameters["ringFadeStart"];
            _RingContrast = effect.Parameters["ringContrast"];
            _RingOpacity = effect.Parameters["ringOpacity"];
            _RingAdaptMid = effect.Parameters["ringAdaptMid"];

            _StripeClipTexture = effect.Parameters["StripeClipTexture"];
            _StripeTexelCount = effect.Parameters["stripeTexelCount"];
            _StripeDirection = effect.Parameters["stripeDirection"];
            _StripePeriod = effect.Parameters["stripePeriod"];
            _StripeBand = effect.Parameters["stripeBand"];
            _StripeClipLevel = effect.Parameters["stripeClipLevel"];
            _StripeAlpha = effect.Parameters["stripeAlpha"];

            _InputLumaAlpha = effect.Parameters["InputLumaAlpha"];
            _AnnotationHSLColor = effect.Parameters["AnnotationHSLColor"];
        }

        public OverlayShaderEffect()
        {
        }
    }
}
