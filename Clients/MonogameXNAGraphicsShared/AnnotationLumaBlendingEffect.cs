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
            CircleVertexColorTextureLumaOverlayEffect
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

        private EffectParameter _InputLumaAlpha;

        private EffectParameter _AnnotationHSLColor;

        public Viewport RenderTargetSize
        {
            set
            {
                _RenderTargetSize.SetValue(new Vector2(value.Width, value.Height));
            }

        }

        public Matrix WorldViewProjMatrix
        {
            get { return _WorldViewProjMatrix.GetValueMatrix(); }
            set { _WorldViewProjMatrix.SetValue(value); }
        }

        public Texture LumaTexture
        {
            set
            {
                _BackgroundTexture.SetValue(value);
            }
        }

        public Texture AnnotationTexture
        {
            set
            {
                _AnnotationTexture.SetValue(value);
            }
            
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

        public EffectTechnique CurrentTechnique
        {
            get
            {
                return this.effect.CurrentTechnique;
            }

        }
        
        public float InputLumaAlphaValue
        {
            get { return _InputLumaAlpha.GetValueSingle(); }
            set { _InputLumaAlpha.SetValue(value); }
        }

        /// <summary>
        /// The color of the annotation in HSL space
        /// </summary>
        public Color AnnotationColorHSL
        {
            get { return _AnnotationHSLColor.GetValueInt32().ToXNAColor(); }
            set { _AnnotationHSLColor.SetValue(value.ToVector4()); }
        }
        
        public void Init(GraphicsDevice device, ContentManager content)
        {
            this.effect = content.Load<Effect>("BillboardAnnotation");
            LoadParameters(this.effect);
            //this.Technique = Techniques.EffectColorLumaOverlayEffect;
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

            _InputLumaAlpha = effect.Parameters["InputLumaAlpha"];
            _AnnotationHSLColor = effect.Parameters["AnnotationHSLColor"];
        }

        public OverlayShaderEffect()
        {
        }
    }
}
