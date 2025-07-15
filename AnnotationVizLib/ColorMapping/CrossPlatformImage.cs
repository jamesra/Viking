using System;
using System.Drawing;
using System.IO;

#if NET9_0
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
#endif

namespace AnnotationVizLib
{
    /// <summary>
    /// Cross-platform image wrapper that provides a unified interface for image operations
    /// across .NET Framework (System.Drawing) and .NET 9.0 (ImageSharp)
    /// </summary>
    public class CrossPlatformImage : IDisposable
    {
#if NET9_0
        private readonly Image<Rgba32> _imageSharpImage;
        private readonly int _width;
        private readonly int _height;
#else
        private readonly Bitmap _systemDrawingImage;
#endif

        public int Width
        {
            get
            {
#if NET9_0
                return _width;
#else
                return _systemDrawingImage.Width;
#endif
            }
        }

        public int Height
        {
            get
            {
#if NET9_0
                return _height;
#else
                return _systemDrawingImage.Height;
#endif
            }
        }

        public CrossPlatformImage(Stream imageStream)
        {
#if NET9_0
            _imageSharpImage = SixLabors.ImageSharp.Image.Load<Rgba32>(imageStream);
            _width = _imageSharpImage.Width;
            _height = _imageSharpImage.Height;
#else
            _systemDrawingImage = new Bitmap(imageStream);
#endif
        }

        public System.Drawing.Color GetPixel(int x, int y)
        {
#if NET9_0
            var pixel = _imageSharpImage[x, y];
            return System.Drawing.Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B);
#else
            return _systemDrawingImage.GetPixel(x, y);
#endif
        }

        public void Dispose()
        {
#if NET9_0
            _imageSharpImage?.Dispose();
#else
            _systemDrawingImage?.Dispose();
#endif
        }
    }
} 