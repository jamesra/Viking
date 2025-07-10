using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 

namespace VikingXNAGraphics
{
    public static class Extensions
    {
        /// <summary>
        /// Load a texture using one texture as the color channel and the other as the alpha
        /// </summary>
        /// <param name="TextureName"></param>
        /// <param name="AlphaName"></param>
        /// <returns></returns>
        public static Texture2D LoadTextureWithAlpha(this ContentManager Content, string TextureName, string AlphaName)
        {
            Texture2D ColorTexture = Content.Load<Texture2D>(TextureName);
            Texture2D AlphaTexture = Content.Load<Texture2D>(AlphaName);

            ColorTexture.ReplaceAlphaChannel(AlphaTexture);

            return ColorTexture;
        }

        public static void ReplaceAlphaChannel(this Texture2D ColorTexture, Texture2D AlphaTexture)
        {
            int ArraySize = ColorTexture.Width * ColorTexture.Height;
            Color[] ColorTextureData = new Color[ArraySize];
            Color[] AlphaTextureData = new Color[ArraySize];

            ColorTexture.GetData<Color>(ColorTextureData);
            AlphaTexture.GetData<Color>(AlphaTextureData);

            for (int i = 0; i < ArraySize; i++)
            {
                ColorTextureData[i] = new Color(ColorTextureData[i].R,
                                                                        ColorTextureData[i].G,
                                                                        ColorTextureData[i].B,
                                                                        AlphaTextureData[i].R);
            }

            ColorTexture.SetData<Color>(ColorTextureData);
        }
    }
}
