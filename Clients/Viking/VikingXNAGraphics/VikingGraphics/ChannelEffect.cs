using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNA
{
    public class ChannelEffect 
    {
        public Effect effect;

        private EffectParameter _WorldMatrix;
        private EffectParameter _ProjectionMatrix;
        private EffectParameter _ViewMatrix;
        private EffectParameter _ChannelColor;

        private EffectParameter _Texture;

        public Matrix WorldMatrix
        {
            get { return _WorldMatrix.GetValueMatrix(); }
            set { _WorldMatrix.SetValue(value); }
        }

        public Matrix ProjectionMatrix
        {
            get { return _ProjectionMatrix.GetValueMatrix(); }
            set { _ProjectionMatrix.SetValue(value); }
        }

        public Matrix ViewMatrix
        {
            get { return _ViewMatrix.GetValueMatrix(); }
            set { _ViewMatrix.SetValue(value); }
        }

        public Color ChannelColor
        {
            get { return new Color(_ChannelColor.GetValueVector4()); }
            set { _ChannelColor.SetValue(value.ToVector4()); }
        }

        public Texture2D Texture
        {
            get { return _Texture.GetValueTexture2D();}
            set { _Texture.SetValue(value); }
        }

        public void CommitChanges()
        {
            effect.CommitChanges(); 
        }
            
        public ChannelEffect(Effect effect)
        {
            this.effect = effect;

            _WorldMatrix = effect.Parameters["World"];
            _ProjectionMatrix = effect.Parameters["Projection"];
            _ViewMatrix = effect.Parameters["View"];
            _ChannelColor = effect.Parameters["ChannelColor"];
            _Texture = effect.Parameters["Texture"]; 

            effect.CurrentTechnique = effect.Techniques["ChannelEffect"];
            
        }
    }
}
