namespace MorphologyMesh
{
    public static class ColladaExtensions
    {
        public static double[] ToElements(this System.Drawing.Color color)
        {
            return new double[] {
                (double)color.R / 255.0,
                (double)color.G / 255.0,
                (double)color.B / 255.0,
                (double)color.A / 255.0
            };
        }
    }
}
