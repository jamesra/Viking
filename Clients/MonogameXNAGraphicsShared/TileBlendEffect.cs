#nullable enable

using Microsoft.Xna.Framework;
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
            get => new(_TileColor.GetValueVector4());
            set
            {
                _TileColor.SetValue(value.ToVector4());

                HSLColor HSLColor = value.GetHSL();
                _TileHue.SetValue((float)(HSLColor.Hue / 360f));

            }
        }

        private readonly EffectParameter? _DisplayMin;
        private readonly EffectParameter? _DisplayMax;

        /// <summary>
        /// The window of texture sample values (0 to 1) shown from black to white. Ordinary tiles use 0 to 1. Crushed tiles hold
        /// codes, so their window expands the codes to the screen. Does nothing for a shader built without the window.
        /// </summary>
        public (float Min, float Max) DisplayWindow
        {
            get => (_DisplayMin?.GetValueSingle() ?? 0f, _DisplayMax?.GetValueSingle() ?? 1f);
            set
            {
                _DisplayMin?.SetValue(value.Min);
                _DisplayMax?.SetValue(value.Max);
            }
        }

        private readonly EffectParameter? _DisplayGamma;

        /// <summary>The gamma applied after the display window: above 1 lifts dark and middle tones, 1 is linear. Does nothing for a shader built without it.</summary>
        public float DisplayGamma
        {
            get => _DisplayGamma?.GetValueSingle() ?? 1f;
            set => _DisplayGamma?.SetValue(value);
        }

        public Matrix WorldViewProjMatrix
        {
            get => _WorldViewProjMatrix.GetValueMatrix();
            set => _WorldViewProjMatrix.SetValue(value);
        }

        public Texture2D Texture
        {
            get => _Texture.GetValueTexture2D();
            set => _Texture.SetValue(value);
        }

        public void RenderToGreyscale() => effect.CurrentTechnique = effect.Techniques["TileLayoutToGreyscaleEffect"];

        public void RenderToHSV() => effect.CurrentTechnique = effect.Techniques["TileLayoutToHSVEffect"];

        public TileLayoutEffect(Effect effect)
        {
            this.effect = effect;

            _WorldViewProjMatrix = effect.Parameters["mWorldViewProj"];

            _Texture = effect.Parameters["Texture"];

            _TileColor = effect.Parameters["TileColor"];
            _TileHue = effect.Parameters["TileHue"];
            _DisplayMin = effect.Parameters["DisplayMin"];
            _DisplayMax = effect.Parameters["DisplayMax"];
            _DisplayGamma = effect.Parameters["DisplayGamma"];

            effect.CurrentTechnique = effect.Techniques["TileLayoutToGreyscaleEffect"];

        }
    }
}
