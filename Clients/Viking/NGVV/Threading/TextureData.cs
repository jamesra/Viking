namespace Viking
{
    /// <summary>
    /// Holder for texture data created on threads so that textures are created only on the main thread
    /// XNA's behavior for multi-threaded GPU use is ambiguous
    /// </summary>
    internal readonly struct TextureData(byte[] data, int width, int height)
    {
        public readonly byte[] pixelBytes = data;
        public readonly int width = width;
        public readonly int height = height;

        public bool IsEmpty => pixelBytes is null;
    }
}
