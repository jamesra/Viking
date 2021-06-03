using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{
    public interface IInitEffect
    {
        void Init(GraphicsDevice device, ContentManager content);
    }
}
