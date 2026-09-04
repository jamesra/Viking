namespace MorphologyMesh
{
    public readonly struct RgbaColor(byte r, byte g, byte b, byte a)
    {
        public byte R { get; } = r;
        public byte G { get; } = g;
        public byte B { get; } = b;
        public byte A { get; } = a;

        public static RgbaColor Empty { get; } = new(0, 0, 0, 0);

        public static RgbaColor FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);

        public static RgbaColor FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);

        public static RgbaColor CornflowerBlue { get; } = FromRgb(100, 149, 237);
    }
}
