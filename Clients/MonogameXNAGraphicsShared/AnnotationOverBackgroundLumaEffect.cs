using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{
    /*
    // A vertex type for drawing RoundLines, including an instance index
    struct AnnotationOverlayVertex
    {
        public Vector3 pos;
        public Vector2 AnnotationTextureCoords;
        public Vector2 ScreenTextureCoords;

        public AnnotationOverlayVertex(Vector3 pos, Vector2 annotationTextureCoords, Vector2 screenTextureCoords)
        {
            this.pos = pos;
            this.AnnotationTextureCoords = annotationTextureCoords;
            this.ScreenTextureCoords = screenTextureCoords;
        }

        public static int SizeInBytes = 7 * sizeof(float);

        public static VertexElement[] VertexElements = new VertexElement[] 
            {
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(3 * sizeof(float), VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
            };
    }
    */

    public class AnnotationOverBackgroundLumaEffect : IInitEffect
    {
        public enum Techniques
        {
            RGBCircleOverBackgroundValueOverlayEffect,
            RGBTextureOverBackgroundValueOverlayEffect
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

        public Techniques Technique
        {
            set
            {
                switch (value)
                {
                    case Techniques.RGBCircleOverBackgroundValueOverlayEffect:
                        effect.CurrentTechnique = effect.Techniques["RGBCircleOverBackgroundValueOverlayEffect"];
                        break;
                    case Techniques.RGBTextureOverBackgroundValueOverlayEffect:
                        effect.CurrentTechnique = effect.Techniques["RGBTextureOverBackgroundValueOverlayEffect"];
                        break;
                    default:
                        throw new ArgumentException("Unknown technique");
                }
            }
        } 

        public EffectTechnique CurrentTechnique
        {
            get
            {
                return this.effect.CurrentTechnique;
            }

        }

        public void AnnotateWithTexture(Texture2D AnnotationTexture)
        {            
            _AnnotationTexture.SetValue(AnnotationTexture);
            Technique = Techniques.RGBTextureOverBackgroundValueOverlayEffect;
        }

        public float InputLumaAlphaValue
        {
            get { return _InputLumaAlpha.GetValueSingle(); }
            set { _InputLumaAlpha.SetValue(value); }
        }

        public void AnnotateWithCircle(float BorderRatio, float inputLumaAlphaValue)
        {
            //  _RadiusSquared.SetValue(Radius * Radius);
            /*
            float BorderStartRadiusSquared = (float)0.5 * (1 - BorderRatio);
            BorderStartRadiusSquared *= BorderStartRadiusSquared;

            float BorderBlendStartRadiusSquared = (float)0.5 * (1 - (2 * BorderRatio));
            BorderBlendStartRadiusSquared *= BorderBlendStartRadiusSquared;

            _BorderStartRadius.SetValue((float)Math.Sqrt(BorderStartRadiusSquared));
            _BorderStartSquared.SetValue(BorderStartRadiusSquared);
            _BorderBlendStartRadius.SetValue((float)Math.Sqrt(BorderBlendStartRadiusSquared));
            _BorderBlendStartSquared.SetValue(BorderBlendStartRadiusSquared); 
            */

            _InputLumaAlpha.SetValue(inputLumaAlphaValue);
            Technique = Techniques.RGBCircleOverBackgroundValueOverlayEffect;
        }

        public void Init(GraphicsDevice device, ContentManager content)
        {
            this.effect = content.Load<Effect>("AnnotationOverlayShader");
            LoadParameters(this.effect);
            this.Technique = Techniques.RGBTextureOverBackgroundValueOverlayEffect;
        }

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
        }

        public AnnotationOverBackgroundLumaEffect()
        {
        }
    }
}
