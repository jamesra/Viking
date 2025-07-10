using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace VikingXNAGraphics
{
    public static class TextureExtensions
    {
        /// <summary>
        /// Returns a sequence of bytes in ARGB order
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public static Byte[] ToRgbBytes(this Texture2D texture)
        {
            byte[] textureData = new byte[texture.Width * texture.Height * 4];
            texture.GetData<byte>(textureData);

            /*Reverse the position of the blue channel*/
            byte blue;
            for (int i = 0; i < textureData.Length; i += 4)
            {
                blue = textureData[i];
                textureData[i] = textureData[i + 2];
                textureData[i + 2] = blue;
            }
            
            return textureData;
        }
    }
}
