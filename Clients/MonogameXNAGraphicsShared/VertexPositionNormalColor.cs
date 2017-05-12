using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{
    public struct VertexPositionNormalColor : IVertexType
    {
        public static VertexDeclaration Declaration = new VertexDeclaration(new VertexElement[]
        {
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0)
        });

        Vector3 vPosition;
        Vector3 vNormal;
        Color vColor;

        public Vector3 Position
        {
            get { return vPosition; }
            set { vPosition = value; }
        }

        public Vector3 Normal
        {
            get { return vNormal; }
            set { vNormal = value; }
        }

        public Color Color
        {
            get { return vColor; }
            set { vColor = value; }
        }

        public VertexPositionNormalColor(Vector3 position, Vector3 normal, Color color)
        {
            vPosition = position;
            vNormal = normal;
            vColor = color; 
        }

        public override bool Equals(object obj)
        {
            VertexPositionNormalColor other = (VertexPositionNormalColor)obj;

            return other.vPosition == this.vPosition &&
                   other.vNormal == this.vNormal &&
                   other.vColor == this.vColor;
        }

        public override int GetHashCode()
        {
            return this.Position.GetHashCode() + this.Position.GetHashCode() + this.Color.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("P: {0} N: {1} C: {2}", this.vPosition, this.vNormal, this.vColor);
        }

        public static bool operator ==(VertexPositionNormalColor left, VertexPositionNormalColor right)
        {
            if (Type.ReferenceEquals(left, right))
                return true;

            if (Type.ReferenceEquals(right, null) || Type.ReferenceEquals(left, null))
                return false;

            return left.vPosition == right.vPosition && left.vNormal == right.vNormal && left.vColor == right.vColor;
        }

        public static bool operator !=(VertexPositionNormalColor left, VertexPositionNormalColor right)
        {
            return (left == right) == false;
        }


        VertexDeclaration IVertexType.VertexDeclaration
        {
            get
            {
                return VertexPositionNormalColor.Declaration;
            }
        }
    }
}
