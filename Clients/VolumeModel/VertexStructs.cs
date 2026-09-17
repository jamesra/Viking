using Geometry;

namespace Viking.VolumeModel
{
    public readonly struct PositionTextureVertex
    {
        public readonly Vector3 Position;
        public readonly Vector2 Texture;
    }

    public readonly struct PositionTextureColorVertex
    {
        public readonly Vector3 Position;
        public readonly Vector2 Texture;
        public readonly Vector3 Color;
    }

    public readonly struct PositionNormalTextureVertex(Vector3 pos, Vector3 norm, Vector2 tex)
    {
        public readonly Vector3 Position = pos;
        public readonly Vector3 Normal = norm;
        public readonly Vector2 Texture = tex;
    }
}
