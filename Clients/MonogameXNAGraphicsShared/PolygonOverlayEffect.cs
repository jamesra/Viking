using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    public class PolygonOverlayEffect : IInitEffect
    {
        public enum Techniques
        {
            ColorPolygonOverBackgroundLumaEffect
        };

        public Effect effect;

        public static implicit operator Effect(PolygonOverlayEffect e) => e.effect;

        private EffectParameter _WorldViewProjMatrix;
        private EffectParameter _BackgroundTexture;
        private EffectParameter _OverlayTexture;
        private EffectParameter _RenderTargetSize;
        private EffectParameter _InputLumaAlpha;

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

        public Viewport RenderTargetSize
        {
            set
            {
                _RenderTargetSize.SetValue(new Vector2(value.Width, value.Height));
            }

        }

        public Techniques Technique
        {
            set
            {
                switch(value)
                {
                    case Techniques.ColorPolygonOverBackgroundLumaEffect:
                        effect.CurrentTechnique = effect.Techniques["ColorPolygonOverBackgroundLumaEffect"];
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

        public float InputLumaAlphaValue
        {
            get { return _InputLumaAlpha.GetValueSingle(); }
            set { _InputLumaAlpha.SetValue(value); }
        }

        public PolygonOverlayEffect()
        { 
        }

        public void Init(GraphicsDevice device, ContentManager content)
        { 
            this.effect = content.Load<Effect>("PolygonOverlayShader");
            this.LoadParameters(this.effect);
            effect.CurrentTechnique = effect.Techniques["ColorPolygonOverBackgroundLumaEffect"];
        }

        private void LoadParameters(Effect effect)
        {
            _WorldViewProjMatrix = effect.Parameters["mWorldViewProj"];
            _BackgroundTexture = effect.Parameters["BackgroundTexture"];
            _OverlayTexture = effect.Parameters["OverlayTexture"];
            _RenderTargetSize = effect.Parameters["RenderTargetSize"];
            _InputLumaAlpha = effect.Parameters["InputLumaAlpha"];
        }
    }
}
