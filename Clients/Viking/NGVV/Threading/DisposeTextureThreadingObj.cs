using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Viking
{
    static class DisposeTextureThreadingObj
    {
        public static System.Threading.Tasks.Task DisposeAsync(this Texture2D texture) => Task.Run(() => DoDispose(texture));

        private static void DoDispose(Texture2D texture)
        {
            if (texture.IsDisposed == false)
            {
                try
                {
                    Global.RemoveTexture(texture);
                    texture.Dispose();
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Exception caught disposing of texture: " + e.ToString(), "TextureUse");
                    throw;
                }
            }
        }
    }

}
