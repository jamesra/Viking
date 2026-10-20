namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Header-only checks on the PNG bytes the capture encoder produced. Never decodes pixels.
    /// </summary>
    internal static class CapturedPng
    {
        /// <summary>
        /// Checks the PNG signature and that the header dimensions equal what the render target held.
        /// The bytes come from our own encoder, so the header is the only thing that can disagree with the render
        /// target; decoding every pixel again to read the same two numbers cost tens of milliseconds and a
        /// megabyte-scale allocation per tile.
        /// </summary>
        public static (bool isValid, string errorMessage) Validate(byte[] pngData, int expectedWidth, int expectedHeight)
        {
            if (pngData is null || pngData.Length == 0)
                return (false, "Image validation failed: null or empty data");

            if (pngData.Length < 8 ||
                pngData[0] != 0x89 || pngData[1] != 0x50 || pngData[2] != 0x4E || pngData[3] != 0x47 ||
                pngData[4] != 0x0D || pngData[5] != 0x0A || pngData[6] != 0x1A || pngData[7] != 0x0A)
            {
                return (false, "Image validation failed: invalid PNG signature");
            }

            if (expectedWidth <= 0 || expectedHeight <= 0)
                return (false, $"Image validation failed: invalid dimensions {expectedWidth}x{expectedHeight}");

            if (!TryReadDimensions(pngData, out int width, out int height))
                return (false, "Image validation failed: PNG header is truncated or has no IHDR chunk");

            if (width != expectedWidth || height != expectedHeight)
            {
                return (false, $"Image validation failed: dimension mismatch. Expected {expectedWidth}x{expectedHeight}, got {width}x{height}");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Reads width and height from the IHDR chunk, which must directly follow the 8-byte signature.
        /// False when the data is too short or the first chunk is not IHDR.
        /// </summary>
        public static bool TryReadDimensions(byte[] pngData, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (pngData is null || pngData.Length < 24)
                return false;

            // Bytes 12-15 are the chunk type: "IHDR".
            if (pngData[12] != 0x49 || pngData[13] != 0x48 || pngData[14] != 0x44 || pngData[15] != 0x52)
                return false;

            width = (pngData[16] << 24) | (pngData[17] << 16) | (pngData[18] << 8) | pngData[19];
            height = (pngData[20] << 24) | (pngData[21] << 16) | (pngData[22] << 8) | pngData[23];
            return true;
        }

        /// <summary>
        /// "WxH" from the IHDR for diagnostics, or "invalid" when the data is not a PNG with a readable header.
        /// </summary>
        public static string DescribeDimensions(byte[] pngData)
        {
            if (pngData is null || pngData.Length < 24 ||
                pngData[0] != 0x89 || pngData[1] != 0x50 || pngData[2] != 0x4E || pngData[3] != 0x47)
            {
                return "invalid";
            }

            return TryReadDimensions(pngData, out int width, out int height) ? $"{width}x{height}" : "invalid";
        }
    }
}
