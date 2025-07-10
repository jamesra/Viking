using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{
    public abstract class EffectBase
    {
        protected Effect effect;

        public EffectBase(Effect effect)
        {
            this.effect = effect;
        }

        public Matrix WorldViewProjMatrix { get; set; } = Matrix.Identity;
    }

    public class PolygonOverlayEffect : EffectBase
    {
        public PolygonOverlayEffect(Effect effect) : base(effect) { }
    }

    public class OverlayShaderEffect : EffectBase
    {
        public OverlayShaderEffect(Effect effect) : base(effect) { }
    }

    public class TileLayoutEffect : EffectBase
    {
        public TileLayoutEffect(Effect effect) : base(effect) { }
    }

    public class MergeHSVImagesEffect : EffectBase
    {
        public MergeHSVImagesEffect(Effect effect) : base(effect) { }
    }

    public class ChannelOverlayEffect : EffectBase
    {
        public ChannelOverlayEffect(Effect effect) : base(effect) { }
    }
} 