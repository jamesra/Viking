using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;

namespace Viking
{
    class DisposeTextureThreadingObj
    {
        Texture texture;

        public DisposeTextureThreadingObj(Texture tex)
        {
            this.texture = tex;
        }

        public void ThreadPoolCallback(Object threadContext)
        {
            if (this.texture.IsDisposed == false)
            {
                try
                {
                    Global.RemoveTexture(this.texture);
                    this.texture.Dispose();
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Exception caught disposing of texture: " + e.ToString(), "TextureUse");
                    throw;
                }
            }

            this.texture = null;
        }
    }

}
