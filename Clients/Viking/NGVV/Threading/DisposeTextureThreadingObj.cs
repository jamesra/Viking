using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna; 
using Microsoft.Xna.Framework; 
using Microsoft.Xna.Framework.Graphics;

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
                    this.texture.Dispose();
                    Global.RemoveTexture(this.texture); 
                }
                catch(Exception e)
                {
                    Trace.WriteLine("Exception caught disposing of texture: " + e.ToString(), "TextureUse"); 
                }
            } 
            
            this.texture = null;
        }
    }
    
}
