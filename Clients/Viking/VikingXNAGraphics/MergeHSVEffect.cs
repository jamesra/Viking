using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics; 

namespace VikingXNA
{
    public class MergeHSVImagesEffect 
    {
        public Effect effect;

        private EffectParameter _WorldViewProjMatrix;

        private EffectParameter _NumTextures;

        private EffectParameter _ChannelHueAlpha;
        private EffectParameter _ChannelHueBeta;

        //Used for RGB merges
        private EffectParameter _ChannelColors;
        private EffectParameter _ChannelColorSum; 

        public readonly int MaxChannels = 4; 
                
        public Matrix WorldViewProjMatrix
        {
            get { return _WorldViewProjMatrix.GetValueMatrix(); }
            set { _WorldViewProjMatrix.SetValue(value); }
        }

        public void MergeHSVImages(Texture2D[] Textures, float[] Alphas, float[] Betas)
        {
            Debug.Assert((Textures.Length == Alphas.Length) && (Textures.Length == Betas.Length));

            this.Textures = Textures; ;
            this.HueAlpha = Alphas; 
            this.HueBeta = Betas;

            this.effect.CurrentTechnique = effect.Techniques["MergeHSVImages"];
        }

        public Vector4 MergeRGBImages(Texture[] Textures, Vector4[] ChannelColors)
        {
            Debug.Assert((Textures.Length == ChannelColors.Length));

            this.Textures = Textures;
            this._ChannelColors.SetValue(ChannelColors); 
            
            //Sum the channel Colors
            float[] ChannelColorSum = new float[4] {0,0,0,0};
            float[] ChannelUseCount = new float[4] {0,0,0,0}; 
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

            Vector4 ChannelWeights = new Vector4();
            /*
            ChannelWeights.X = ChannelUseCount[0] >  0 ? ChannelColorSum[0] / (float)ChannelUseCount[0] : 0;
            ChannelWeights.Y = ChannelUseCount[1] > 0 ? ChannelColorSum[1] / (float)ChannelUseCount[1] : 0;
            ChannelWeights.Z = ChannelUseCount[2] > 0 ? ChannelColorSum[2] / (float)ChannelUseCount[2] : 0; 
             */
            ChannelWeights.X = ChannelUseCount[0];
            ChannelWeights.Y = ChannelUseCount[1];
            ChannelWeights.Z = ChannelUseCount[2];
            ChannelWeights.W = ChannelUseCount[3];

            _ChannelColorSum.SetValue(ChannelUseCount); 

            this.effect.CurrentTechnique = effect.Techniques["MergeRGBImages"];

            return ChannelWeights; 
        }
        
        private Texture[] Textures
        {
            set {
                string TextureName = "Texture";
                for (int i = 0; i < value.Length; i++)
                {
                    string ParameterName = TextureName + (i+1).ToString();
                    EffectParameter effectParam = effect.Parameters[ParameterName];
                    effectParam.SetValue(value[i]);

                    if (i >= MaxChannels)
                    {
                        break;
                    }
                }

                if (value.Length >= MaxChannels)
                    _NumTextures.SetValue(MaxChannels);
                else
                    _NumTextures.SetValue(value.Length);
            }
        }

        private float[] HueAlpha
        {
            set
            {
                _ChannelHueAlpha.SetValue(value); 
            }
        }

        private float[] HueBeta
        {
            set
            {
                _ChannelHueBeta.SetValue(value); 
            }
        }

        public MergeHSVImagesEffect(Effect effect)
        {
            this.effect = effect;

            _WorldViewProjMatrix = effect.Parameters["mWorldViewProj"];

            _NumTextures = effect.Parameters["NumTextures"]; 

            _ChannelHueAlpha = effect.Parameters["ChannelHueAlpha"];
            _ChannelHueBeta = effect.Parameters["ChannelHueBeta"];

            _ChannelColors = effect.Parameters["ChannelRGBColor"];
            _ChannelColorSum = effect.Parameters["ChannelRGBColorTotal"];
            
            effect.CurrentTechnique = effect.Techniques["MergeHSVImages"];
            
        }
    }
}
