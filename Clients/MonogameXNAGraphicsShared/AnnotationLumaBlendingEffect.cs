using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{
    public class OverlayShaderEffect : IInitEffect
    {
        public enum Techniques
        {
            /// <summary>
            /// Color defined by effect. Luma blend defined by effect.
            /// </summary>
            EffectColorOverBackgroundValueOverlayEffect,
            /// <summary>
            /// Color defined by effect.  Greyscale + Alpha texture defines blending
            /// Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
            /// </summary>
            EffectColorTextureOverBackgroundValueOverlayEffect,
            /// <summary>
            /// Color defined by vertex. Luma blend defined by effect.
            /// </summary>
            VertexColorOverBackgroundValueOverlayEffect,
            /// <summary>
            /// Color defined by vertex, Texture defines blending
            /// </summary>
            VertexColorTextureOverBackgroundValueOverlayEffect,
            /// <summary>
            /// Rendering limited to unit circle.  Color defined by effect. Luma blend defined by effect.
            /// </summary>
            CircleEffectColorOverBackgroundValueOverlayEffect,
            /// <summary>
            /// Rendering limited to unit circle.  Color defined by effect.  Texture defines luma blending
            /// </summary>
            CircleEffectColorTextureOverBackgroundValueOverlayEffect,
            /// <summary>
            /// Rendering limited to unit circle.  Color defined by vertex. Luma blend defined by effect.
            /// </summary>
            CircleVertexColorOverBackgroundValueOverlayEffect,
            /// <summary>
            /// Rendering limited to unit circle.  Color defined by vertex.  Texture defines luma blending
            /// </summary>
            CircleVertexColorTextureOverBackgroundValueOverlayEffect
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
                    case Techniques.RGBCircleOverBackgroundValueOverlayEffect:
                        effect.CurrentTechnique = effect.Techniques["RGBCircleOverBackgroundValueOverlayEffect"];
                        break;
                    case Techniques.RGBTextureOverBackgroundValueOverlayEffect:
                        effect.CurrentTechnique = effect.Techniques["RGBTextureOverBackgroundValueOverlayEffect"];
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
            //this.Technique = Techniques.EffectColorOverBackgroundValueOverlayEffect;
        }

        /*
        public static OverlayShaderEffect WrapEffect(Effect effect)
        {
            OverlayShaderEffect obj = new OverlayShaderEffect();

            obj.effect = effect;
            obj.LoadParameters(effect);
            obj.Technique = Techniques.RGBTextureOverBackgroundValueOverlayEffect;

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
