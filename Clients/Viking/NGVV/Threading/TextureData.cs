namespace Viking
{
    /// <summary>
    /// Holder for texture data created on threads so that textures are created only on the main thread
    /// XNA's behavior for multi-threaded GPU use is ambiguous
    /// </summary>
    internal readonly struct TextureData
    {
        public readonly byte[] pixelBytes;
        public readonly int width;
        public readonly int height;

        public bool IsEmpty => pixelBytes == null;
        
        public TextureData(byte[] data, int width, int height)
        {
            this.pixelBytes = data;
            this.width = width;
            this.height = height;
        }
    }
}
