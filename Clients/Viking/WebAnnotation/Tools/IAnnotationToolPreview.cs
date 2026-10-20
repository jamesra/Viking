using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// A tool that draws feedback over the annotations while it is active: a shape being placed, a measurement line, the
    /// annotation under the pointer. <see cref="AnnotationScene"/> calls it after drawing the section's annotations.
    /// </summary>
    public interface IAnnotationToolPreview
    {
        void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect);
    }
}
