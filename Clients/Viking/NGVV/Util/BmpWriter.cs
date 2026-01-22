using Microsoft.Xna.Framework.Graphics;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using VikingXNAGraphics;

namespace Viking.Common
{
    //This was added as a workaround for the SaveAsPng memory leak in XNA.Texture2D
    public static class BmpWriter
    {
        /// <summary>
        /// Convert a texture to a bitmap
        /// </summary>
        /// <param name="textureData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static Bitmap ToBmp(this Texture2D texture)
        {
            Byte[] textureData = texture.ToRgbBytes();
            return textureData.ToBmp(texture.Width, texture.Height);
        }

        /// <summary>
        /// Convert a texture to a Bitmap
        /// </summary>
        /// <param name="textureData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static Task<Bitmap> ToBmpAsync(this Texture2D texture)
        {
            Byte[] textureData = texture.ToRgbBytes();
            return ToBmpAsync(textureData, texture.Width, texture.Height);
        }

        /// <summary>
        /// Save a texture as a file
        /// </summary>
        /// <param name="textureData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static void Save(this Texture2D texture, String filename, ImageFormat? format = null)
        {
            Byte[] textureData = texture.ToRgbBytes();
            textureData.SaveBmp(texture.Width, texture.Height, filename, format ?? ImageFormat.Png);
        }

        /// <summary>
        /// Save a texture as a file
        /// </summary>
        /// <param name="textureData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static Task SaveAsync(this Texture2D texture, String filename, ImageFormat? format = null)
        {
            Byte[] textureData = texture.ToRgbBytes();
            return SaveBmpAsync(textureData, texture.Width, texture.Height, filename, format);
        }


        /// <summary>
        /// Convert ARGB bytes to a bitmap
        /// </summary>
        /// <param name="textureData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static Bitmap ToBmp(this byte[] textureData, int width, int height)
        {
            BitmapData lockedBmpData = null;
            Bitmap bmp = new(
                width, height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {

                Rectangle rect = new(0, 0, bmp.Width, bmp.Height);
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
                        System.Runtime.InteropServices.Marshal.Copy(textureData, iBmpRow * 4 * bmp.Width, lockedBmpData.Scan0 + (lockedBmpData.Stride * iBmpRow), 4 * bmp.Width);
                    }
                }

                bmp.UnlockBits(lockedBmpData);
                lockedBmpData = null;
            }
            catch (Exception e)
            {
                if (lockedBmpData != null)
                    bmp.UnlockBits(lockedBmpData);

                throw;
            }

            return bmp;
        }

        /// <summary>
        /// Convert ARGB bytes to a bitmap
        /// </summary>
        /// <param name="textureData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static Task<Bitmap> ToBmpAsync(this byte[] textureData, int width, int height) => Task.Run(() => ToBmp(textureData, width, height));

        /// <summary>
        /// Save ARGB bytes to a bitmap file
        /// </summary>
        /// <param name="textureData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static Task SaveBmpAsync(this byte[] textureData, int width, int height, string filename, ImageFormat? format = null) => Task.Run(() => SaveBmp(textureData, width, height, filename, format));

        public static void SaveBmp(this byte[] textureData, int width, int height, string filename, ImageFormat? format = null)
        {
            format ??= ImageFormat.Png;

            using Bitmap bmp = textureData.ToBmp(width, height);
            bmp.Save(filename, format);
        }
    }
}
