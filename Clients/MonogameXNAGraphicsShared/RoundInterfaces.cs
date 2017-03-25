using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace VikingXNAGraphics
{
    public interface IInitEffect
    {
        void Init(GraphicsDevice device, ContentManager content);
    }
}
