using Microsoft.Xna.Framework.Graphics;

namespace Viking.Rendering
{
    /// <summary>
    /// Section annotation draw/hit-test used by SectionSceneRenderer so Viking.Scene
    /// does not reference WebAnnotation types.
    /// </summary>
    public interface IAnnotationScene
    {
        void Draw(GraphicsDevice device, VikingXNA.Scene scene, int sectionNumber, Texture backgroundLuma, Texture backgroundColors, ref int nextStencilValue);

        object HitTest(int sectionNumber, Geometry.Vector2 worldPosition, out double distance);
    }
}
