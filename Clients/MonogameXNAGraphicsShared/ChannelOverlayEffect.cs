﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace VikingXNA
{
    public class ChannelOverlayEffect 
    {
        public Effect effect;

        private readonly EffectParameter _WorldViewProjMatrix;

        private readonly EffectParameter _BackgroundTexture;
        private readonly EffectParameter _OverlayTexture;

        public Matrix WorldViewProjMatrix
        {
            get { return _WorldViewProjMatrix.GetValueMatrix(); }
            set { _WorldViewProjMatrix.SetValue(value); }
        }

        public void SetEffectTextures(Texture Background, Texture ColorOverlay)
        {
            _BackgroundTexture.SetValue(Background);
            _OverlayTexture.SetValue(ColorOverlay);

            if (Background != null && ColorOverlay != null)
                effect.CurrentTechnique = effect.Techniques["HSOverBackgroundValueOverlayEffect"];
            else if (Background == null)
                effect.CurrentTechnique = effect.Techniques["HSVOnlyOverlayEffect"];
            else if (ColorOverlay == null)
                effect.CurrentTechnique = effect.Techniques["BackgroundOnlyOverlayEffect"]; 
        }
           
        public ChannelOverlayEffect(Effect effect)
        {
            this.effect = effect;

            _WorldViewProjMatrix = effect.Parameters["mWorldViewProj"];
            _BackgroundTexture = effect.Parameters["BackgroundTexture"]; 
            _OverlayTexture = effect.Parameters["OverlayTexture"];

            effect.CurrentTechnique = effect.Techniques["HSOverBackgroundValueOverlayEffect"];
        }
    }
}
