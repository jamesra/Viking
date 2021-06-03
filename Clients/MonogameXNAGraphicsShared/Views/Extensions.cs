using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        public static Texture2D LoadTextureWithAlpha(this Microsoft.Xna.Framework.Content.ContentManager Content, string TextureName, string AlphaName)
        {
            Texture2D ColorTexture = Content.Load<Texture2D>(TextureName);
            Texture2D AlphaTexture = Content.Load<Texture2D>(AlphaName);

            ColorTexture.ReplaceAlphaChannel(AlphaTexture);

            return ColorTexture;
        }

        public static void ReplaceAlphaChannel(this Texture2D ColorTexture, Texture2D AlphaTexture)
        {
            int ArraySize = ColorTexture.Width * ColorTexture.Height;
            Microsoft.Xna.Framework.Color[] ColorTextureData = new Microsoft.Xna.Framework.Color[ArraySize];
            Microsoft.Xna.Framework.Color[] AlphaTextureData = new Microsoft.Xna.Framework.Color[ArraySize];

            ColorTexture.GetData<Microsoft.Xna.Framework.Color>(ColorTextureData);
            AlphaTexture.GetData<Microsoft.Xna.Framework.Color>(AlphaTextureData);

            for (int i = 0; i < ArraySize; i++)
            {
                ColorTextureData[i] = new Microsoft.Xna.Framework.Color(ColorTextureData[i].R,
                                                                        ColorTextureData[i].G,
                                                                        ColorTextureData[i].B,
                                                                        AlphaTextureData[i].R);
            }

            ColorTexture.SetData<Microsoft.Xna.Framework.Color>(ColorTextureData);
        }
    }
}
