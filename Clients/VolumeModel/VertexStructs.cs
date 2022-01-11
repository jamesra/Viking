using Geometry;

namespace Viking.VolumeModel
{
    public readonly struct PositionTextureVertex
    {
        public readonly GridVector3 Position;
        public readonly GridVector2 Texture;
    }

    public readonly struct PositionTextureColorVertex
    {
        public readonly GridVector3 Position;
        public readonly GridVector2 Texture;
        public readonly GridVector3 Color;
    }

    public readonly struct PositionNormalTextureVertex
    {
        public readonly GridVector3 Position;
        public readonly GridVector3 Normal;
        public readonly GridVector2 Texture;

        public PositionNormalTextureVertex(GridVector3 pos, GridVector3 norm, GridVector2 tex)
        {
            Position = pos;
            Normal = norm;
            Texture = tex;
        }
    }
}
