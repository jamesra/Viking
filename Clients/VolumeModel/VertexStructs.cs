using Geometry;

namespace Viking.VolumeModel
{
    public struct PositionTextureVertex
    {
        public GridVector3 Position;
        public GridVector2 Texture;
    }

    public struct PositionTextureColorVertex
    {
        public GridVector3 Position;
        public GridVector2 Texture;
        public GridVector3 Color;
    }

    public struct PositionNormalTextureVertex
    {
        public GridVector3 Position;
        public GridVector3 Normal;
        public GridVector2 Texture;

        public PositionNormalTextureVertex(GridVector3 pos, GridVector3 norm, GridVector2 tex)
        {
            Position = pos;
            Normal = norm;
            Texture = tex;
        }
    }
}
