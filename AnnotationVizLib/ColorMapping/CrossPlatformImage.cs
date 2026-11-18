using System;
using System.Drawing;
using System.IO;

#if NET9_0
using SkiaSharp;
#endif

namespace AnnotationVizLib
{
    /// <summary>
    /// Cross-platform image wrapper that provides a unified interface for image operations
    /// across .NET Framework (System.Drawing) and .NET 9.0 (SkiaSharp)
    /// </summary>
    public class CrossPlatformImage : IDisposable
    {
#if NET9_0
        private readonly SKBitmap _skiaImage;
        private readonly int _width;
        private readonly int _height;
#else
        private readonly Bitmap _systemDrawingImage;
#endif

        public int Width =>
#if NET9_0
                _width;
#else
                _systemDrawingImage.Width;
#endif


        public int Height =>
#if NET9_0
                _height;
#else
                _systemDrawingImage.Height;
#endif


        public CrossPlatformImage(Stream imageStream)
        {
#if NET9_0
            _skiaImage = SKBitmap.Decode(imageStream) ?? throw new ArgumentException("The stream is not an image SkiaSharp can decode.", nameof(imageStream));
            _width = _skiaImage.Width;
            _height = _skiaImage.Height;
#else
            _systemDrawingImage = new Bitmap(imageStream);
#endif
        }

        public System.Drawing.Color GetPixel(int x, int y)
        {
#if NET9_0
            SKColor pixel = _skiaImage.GetPixel(x, y);
            return System.Drawing.Color.FromArgb(pixel.Alpha, pixel.Red, pixel.Green, pixel.Blue);
#else
            return _systemDrawingImage.GetPixel(x, y);
#endif
        }

        public void Dispose() =>
#if NET9_0
            _skiaImage?.Dispose();
#else
            _systemDrawingImage?.Dispose();
#endif

    }
}