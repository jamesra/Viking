using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using VikingXNAGraphics;


namespace VikingXNA
{     
    public readonly struct ChannelSumResult
    {
        public readonly float[] ChannelColorSum { get;  }
        public readonly int[] ChannelUseCount { get;  } 
        
        public ChannelSumResult(in float[] channelColorSum, in int[] channelUseCount)
        {
            ChannelColorSum = channelColorSum;
            ChannelUseCount = channelUseCount;
        }
    }

    public class MergeHSVImagesEffect 
    {
        public Effect effect;

        private readonly EffectParameter _WorldViewProjMatrix;
          
        private readonly EffectParameter _ChannelHueAlpha;
        private readonly EffectParameter _ChannelHueBeta;

        //Used for RGB merges
        private readonly EffectParameter _OverlayColor;
        private readonly EffectParameter _OverlayColorScalar;

        private readonly EffectParameter _BaseTexture;
        private readonly EffectParameter _OverlayTexture;
        
        private readonly EffectParameter _OverlayChannelTotals;

        public readonly int MaxChannels = 4;


        public float HueAlpha
        {
            get => _ChannelHueAlpha.GetValueSingle();
            set => _ChannelHueAlpha.SetValue(value);
        }

        public float HueBeta
        {
            get => _ChannelHueBeta.GetValueSingle();
            set => _ChannelHueBeta.SetValue(value);
        }

        public Color OverlayColor
        {
            get => _OverlayColor.GetValueVector4().ToColor();
            set => _OverlayColor.SetValue(value.ToVector4());
        }

        public Vector4 OverlayColorScalar
        {
            get => _OverlayColorScalar.GetValueVector4();
            set => _OverlayColorScalar.SetValue(value);
        }

        public Texture2D BaseTexture
        {
            get => _BaseTexture.GetValueTexture2D();
            set => _BaseTexture.SetValue(value);
        }
        
        public Texture2D OverlayTexture
        {
            get => _OverlayTexture.GetValueTexture2D();
            set => _OverlayTexture.SetValue(value);
        }

        public Matrix WorldViewProjMatrix
        {
            get => _WorldViewProjMatrix.GetValueMatrix();
            set => _WorldViewProjMatrix.SetValue(value);
        }
        
        public Vector4 OverlayChannelTotals
        {
            get => _OverlayChannelTotals.GetValueVector4();
            set => _OverlayChannelTotals.SetValue(value);
        }
        
        public void PrepareHCLToRGB(Texture2D texture)
        {
            this.effect.CurrentTechnique = effect.Techniques["HCLToRGB"]; 
        }

        public void PrepareRGBToHCL(Texture2D texture)
        {
            this.effect.CurrentTechnique = effect.Techniques["RGBToHCL"];
        }

        public void PrepareMergeHSVImages(Texture2D BaseTexture, Texture2D OverlayTexture, float Alpha, float Beta)
        { 
            this.HueAlpha = Alpha; 
            this.HueBeta = Beta;

            this.effect.CurrentTechnique = effect.Techniques["MergeHSVImages"];
        }


        /// <summary>
        /// This function merges multiple RGB images into a single image based on the provided channel colors.
        /// </summary>
        /// <param name="BaseTexture"></param>
        /// <param name="OverlayTexture"></param>
        /// <param name="ChannelColors"></param>
        /// <param name="ChannelColorSums">The sum of all ChannelColors that will be blended.</param>
        /// <returns></returns>
        public void PrepareMergeRGBImage(Texture2D BaseTexture, Texture2D OverlayTexture, Color OverlayColor)
        {  
            this.BaseTexture = BaseTexture;
            this.OverlayTexture = OverlayTexture; 
              
            var weighted_colors = OverlayColor.ToVector4();
            
            /*
            for(int i = 0; i < 4; i++)
            {
                if (ChannelColorSum[i] == 0)
                    weighted_colors[i] = ChannelInUse[i] ? weighted_colors[i] : 0;
                else
                    weighted_colors[i] = ChannelInUse[i] ? weighted_colors[i] / ChannelColorSum[i] : 0; 
            }
            */

            this._OverlayColor.SetValue(weighted_colors);
              
            this.effect.CurrentTechnique = effect.Techniques["SumRGBImages"];

            return; 
        }
        
        public void PrepareNormalize(Texture2D inputTexture, Vector4 channelTotals)
        {
            this.BaseTexture = inputTexture;
            this.OverlayChannelTotals = channelTotals;
            this.effect.CurrentTechnique = effect.Techniques["NormalizeByTotal"];
        }

        
        public static ChannelSumResult CalculateChannelTotals(Vector4[] ChannelColors)
        {
            //Sum the channel Colors
            float[] ChannelColorSum = new float[4] {0,0,0,0};
            int[] ChannelUseCount = new int[4] {0,0,0,0}; 
            foreach (Vector4 c in ChannelColors)
            {
                ChannelColorSum[0] += (float)c.X;
                ChannelColorSum[1] += (float)c.Y;
                ChannelColorSum[2] += (float)c.Z;
                ChannelColorSum[3] += (float)c.W;

                ChannelUseCount[0] += c.X > 0 ? 1 : 0;
                ChannelUseCount[1] += c.Y > 0 ? 1 : 0;
                ChannelUseCount[2] += c.Z > 0 ? 1 : 0;
                ChannelUseCount[3] += c.W > 0 ? 1 : 0; 
            }
            
            var result = new ChannelSumResult(ChannelColorSum, ChannelUseCount);

            return result;
        }


        public void SetTextures(Texture2D baseTexture, Texture2D overlayTexture)
        {
            _BaseTexture.SetValue(baseTexture);
            _OverlayTexture.SetValue(overlayTexture);
        }

        public MergeHSVImagesEffect(Effect effect)
        {
            this.effect = effect;

            _WorldViewProjMatrix = effect.Parameters["mWorldViewProj"]; 
            _ChannelHueAlpha = effect.Parameters["ChannelHueAlpha"];
            _ChannelHueBeta = effect.Parameters["ChannelHueBeta"];  
            _BaseTexture = effect.Parameters["BackgroundTexture"];
            _OverlayTexture = effect.Parameters["OverlayTexture"];
            _OverlayChannelTotals = effect.Parameters["OverlayChannelTotals"];
            _OverlayColor = effect.Parameters["OverlayColor"];
            _OverlayColorScalar = effect.Parameters["OverlayColorScalar"];

            effect.CurrentTechnique = effect.Techniques["MergeHSVImages"];
            
        }
    }
}
