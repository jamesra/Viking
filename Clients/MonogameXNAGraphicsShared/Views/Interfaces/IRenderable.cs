using Microsoft.Xna.Framework.Graphics;
using RoundCurve;
using RoundLineCode;
using System;
using System.ComponentModel;
using VikingXNA;

namespace VikingXNAGraphics
{
    public enum OverlayStyle
    {
        /// <summary>
        /// Alpha blending is used to draw over existing data on the render surface
        /// </summary>
        Alpha,
        /// <summary>
        /// Vikings custom luma shaders are used to draw over existing grayscale textures on the render surface.  Low luma values remain dark, high luma values aquire color.
        /// </summary>
        Luma

    }
    public interface IRenderable
    {
        /// <summary>
        /// This should be implemented to call a static method that can render an array of IRenderable that have the same underlying type as the class implementing IRenderable
        /// </summary>
        /// <param name="device"></param>
        /// <param name="scene"></param>
        /// <param name="items"></param>
        void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle Overlay, IRenderable[] items);

        /// <summary>
        /// Draws this instance only
        /// </summary>
        /// <param name="device"></param>
        /// <param name="scene"></param>
        void Draw(GraphicsDevice device, IScene scene, OverlayStyle Overlay);
    }


    public static class RenderableExtensions
    {
        public static RoundLineCode.RoundLineManager GetLineManager(this OverlayStyle overlay, GraphicsDevice device)
        {
            return overlay switch
            {
                OverlayStyle.Alpha => VikingXNAGraphics.DeviceEffectsStore<RoundLineManager>.TryGet(device),
                OverlayStyle.Luma => VikingXNAGraphics.DeviceEffectsStore<LumaOverlayRoundLineManager>.TryGet(device),
                _ => throw new NotImplementedException(string.Format("GetLineManager: Unknown Overlay Style {0}", overlay)),
            };
        }

        public static RoundCurve.CurveManager GetCurveManager(this OverlayStyle overlay, GraphicsDevice device)
        {
            return overlay switch
            {
                OverlayStyle.Alpha => VikingXNAGraphics.DeviceEffectsStore<RoundCurve.CurveManager>.TryGet(device),
                OverlayStyle.Luma => VikingXNAGraphics.DeviceEffectsStore<RoundCurve.CurveManagerHSV>.TryGet(device),
                _ => throw new NotImplementedException(string.Format("GetCurveManager: Unknown Overlay Style {0}", overlay)),
            };
        }
    }
}
