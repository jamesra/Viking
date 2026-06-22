using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace VikingXNAGraphics
{
    [DataContract]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VertexPositionNormalColor(Vector3 position, Vector3 normal, Color color) : IVertexType
    {
        /// <summary>Stride in bytes: Vector3 (12) + Vector3 (12) + Color (4) = 28.</summary>
        public static readonly int StrideBytes = 28;

        public static VertexDeclaration Declaration = new(StrideBytes,
        [
            new(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new(24, VertexElementFormat.Color, VertexElementUsage.Color, 0)
        ]);

        [DataMember]
        Vector3 vPosition = position;
        [DataMember]
        Vector3 vNormal = normal;
        [DataMember]
        Color vColor = color;

        public readonly Vector3 Position => vPosition;

        public Vector3 Normal
        {
            readonly get => vNormal;
            set => vNormal = value;
        }

        public Color Color
        {
            readonly get => vColor;
            set => vColor = value;
        }

        public override readonly bool Equals(object obj)
        {
            VertexPositionNormalColor other = (VertexPositionNormalColor)obj;

            return other.vPosition == this.vPosition &&
                   other.vNormal == this.vNormal &&
                   other.vColor == this.vColor;
        }

        public override readonly int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Position.GetHashCode();
                hash = hash * 23 + Normal.GetHashCode();
                hash = hash * 23 + Color.GetHashCode();
                return hash;
            }
        }

        public override readonly string ToString() => string.Format("P: {0} N: {1} C: {2}", this.vPosition, this.vNormal, this.vColor);

        public static bool operator ==(VertexPositionNormalColor left, VertexPositionNormalColor right)
        {
            if (left.Equals(right))
                return true;

            //if (right is null || left is null)
            //    return false;

            return left.vPosition == right.vPosition && left.vNormal == right.vNormal && left.vColor == right.vColor;
        }

        public static bool operator !=(VertexPositionNormalColor left, VertexPositionNormalColor right) => !(left.Equals(right));


        readonly VertexDeclaration IVertexType.VertexDeclaration => VertexPositionNormalColor.Declaration;
    }
}
