﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic; 
using VikingXNAGraphics;

namespace VikingXNA
{
    public class TileLayoutEffect 
    {
        public Effect effect;

        private readonly EffectParameter _WorldViewProjMatrix;

        private readonly EffectParameter _Texture;

        private readonly EffectParameter _TileColor;
        private readonly EffectParameter _TileHue;

        public Color TileColor
        {
            get { return new Color(_TileColor.GetValueVector4()); }
            set { 
                _TileColor.SetValue(value.ToVector4());

                HSLColor HSLColor = value.GetHSL();
                _TileHue.SetValue((float)(HSLColor.Hue / 360f));
                
                }
        }

        public Matrix WorldViewProjMatrix
        {
            get { return _WorldViewProjMatrix.GetValueMatrix(); }
            set { _WorldViewProjMatrix.SetValue(value); }
        }

        public Texture2D Texture
        {
            get { return _Texture.GetValueTexture2D();}
            set { _Texture.SetValue(value); }
        }

        public void RenderToGreyscale()
        {
            effect.CurrentTechnique = effect.Techniques["TileLayoutToGreyscaleEffect"];
        }

        public void RenderToHSV()
        {
            effect.CurrentTechnique = effect.Techniques["TileLayoutToHSVEffect"];
        }

        public TileLayoutEffect(Effect effect)
        {
            this.effect = effect;

            _WorldViewProjMatrix = effect.Parameters["mWorldViewProj"];

            _Texture = effect.Parameters["Texture"];

            _TileColor = effect.Parameters["TileColor"];
            _TileHue = effect.Parameters["TileHue"];

            effect.CurrentTechnique = effect.Techniques["TileLayoutToGreyscaleEffect"];
            
        }
    }
}
