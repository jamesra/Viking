using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using VikingXNAGraphics;

namespace VikingXNAGraphics
{

    /// <summary>
    /// A catch all that has all of the objects I usually need to render a scene onto a graphics device
    /// </summary>
    public interface IRenderInfo : IPrimitiveRenderInfo, ILabelRenderInfo
    {
    }
     
    public interface IPrimitiveRenderInfo
    {
        GraphicsDevice device { get; }

        BasicEffect basicEffect { get; }

        OverlayShaderEffect overlayEffect { get; }
    }

    public interface ILabelRenderInfo
    {
        SpriteBatch spriteBatch { get; }

        SpriteFont font { get; }
    }

}
