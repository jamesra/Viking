using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNA
{
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

    public class AnnotationOverBackgroundLumaEffect
    {
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

        public void AnnotateWithTexture(Texture2D AnnotationTexture)
        {            
            _AnnotationTexture.SetValue(AnnotationTexture);
            effect.CurrentTechnique = effect.Techniques["RGBOverBackgroundValueOverlayEffect"];
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
            effect.CurrentTechnique = effect.Techniques["RGBCircleOverBackgroundValueOverlayEffect"]; 
        }


        public AnnotationOverBackgroundLumaEffect(Effect effect)
        {
            this.effect = effect;

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

            effect.CurrentTechnique = effect.Techniques["RGBOverBackgroundValueOverlayEffect"];
        }
    }
}
