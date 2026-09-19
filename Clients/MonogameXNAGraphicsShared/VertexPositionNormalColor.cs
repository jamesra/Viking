using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace VikingXNAGraphics
{
    [DataContract]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VertexPositionNormalColor(Vector3 position, Vector3 normal, Color color) : IVertexType, IEquatable<VertexPositionNormalColor>
    {
        public static VertexDeclaration Declaration = new(
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

        public override readonly bool Equals(object obj) => obj is VertexPositionNormalColor other && Equals(other);

        public readonly bool Equals(VertexPositionNormalColor other) =>
            vPosition == other.vPosition &&
            vNormal == other.vNormal &&
            vColor == other.vColor;

        public override readonly int GetHashCode() => this.Position.GetHashCode() + this.Position.GetHashCode() + this.Color.GetHashCode();

        public override readonly string ToString() => string.Format("P: {0} N: {1} C: {2}", this.vPosition, this.vNormal, this.vColor);

        public static bool operator ==(VertexPositionNormalColor left, VertexPositionNormalColor right) => left.Equals(right);

        public static bool operator !=(VertexPositionNormalColor left, VertexPositionNormalColor right) => !left.Equals(right);


        readonly VertexDeclaration IVertexType.VertexDeclaration => VertexPositionNormalColor.Declaration;
    }
}
