using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Xna.Framework.Graphics;

namespace Viking.Common
{
   //This was added as a workaround for the SaveAsPng memory leak in XNA.Texture2D
    public class BmpWriter
    {
        byte[] textureData;
        Bitmap bmp;
        BitmapData bitmapData;
        IntPtr safePtr;
        Rectangle rect;
        public ImageFormat imageFormat;
        public BmpWriter(int width, int height)
        {
            textureData = new byte[4 * width * height];

            bmp = new System.Drawing.Bitmap(
                            width, height,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb
                            );

            rect = new System.Drawing.Rectangle(0, 0, width, height);

            imageFormat = System.Drawing.Imaging.ImageFormat.Png;
        }

        public void TextureToBmp(Texture2D texture, String filename)
        {
            texture.GetData<byte>(textureData);
            byte blue;
            for (int i = 0; i < textureData.Length; i += 4)
            {
                blue = textureData[i];
                textureData[i] = textureData[i + 2];
                textureData[i + 2] = blue;
            }

            bitmapData = bmp.LockBits(
                            rect,
                            System.Drawing.Imaging.ImageLockMode.WriteOnly,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb
                            );

            safePtr = bitmapData.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(textureData, 0, safePtr, textureData.Length);
            bmp.UnlockBits(bitmapData);

            bmp.Save(filename, imageFormat);

        }

        static public void TextureToBmpAsync(Texture2D texture, String filename)
        {
            using (Bitmap bmp = new System.Drawing.Bitmap(
                            texture.Width, texture.Height,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb
                        ))
            {
                Byte[] textureData = new byte[4 * texture.Width * texture.Height];
                texture.GetData<byte>(textureData);

                /*Reverse the position of the blue channel*/
                byte blue;
                for (int i = 0; i < textureData.Length; i += 4)
                {
                    blue = textureData[i];
                    textureData[i] = textureData[i + 2];
                    textureData[i + 2] = blue;
                }

                BitmapData lockedBmpData;
                Rectangle rect = new System.Drawing.Rectangle(0, 0, texture.Width, texture.Height);
                lockedBmpData = bmp.LockBits(
                                rect,
                                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                                System.Drawing.Imaging.PixelFormat.Format32bppArgb
                                );

                IntPtr safePtr;
                safePtr = lockedBmpData.Scan0;

                //The easy case...
                if (lockedBmpData.Stride == lockedBmpData.Width * 4) 
                    System.Runtime.InteropServices.Marshal.Copy(textureData, 0, safePtr, textureData.Length);
                else
                {
                    for (int iBmpRow = 0; iBmpRow < lockedBmpData.Height; iBmpRow++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(textureData, iBmpRow * 4 * texture.Width, lockedBmpData.Scan0 + (lockedBmpData.Stride * iBmpRow), 4 * texture.Width);
                    }
                }

                bmp.UnlockBits(lockedBmpData);

                bmp.Save(filename, ImageFormat.Png);
            }
        }
    } 
}
