namespace Viking
{
    /// <summary>
    /// Holder for texture data created on threads so that textures are created only on the main thread
    /// XNA's behavior for multi-threaded GPU use is ambiguous
    /// </summary>
    internal readonly struct TextureData(byte[] data, int width, int height, double? crushGain = null)
    {
        public readonly byte[] pixelBytes = data;
        public readonly int width = width;
        public readonly int height = height;

        /// <summary>The tile's gain from its <c>nornir_crush</c> PNG chunk, or null when it has none.</summary>
        public readonly double? crushGain = crushGain;

        public bool IsEmpty => pixelBytes is null;
    }
}
