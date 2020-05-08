using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics
{ 
    public interface IMeshModel<VERTEXTYPE>
         where VERTEXTYPE : struct, IVertexType
    {
        VERTEXTYPE[] Verticies { get; }
        int[] Edges { get; }
    }

    public class MeshModel<VERTEXTYPE> : IMeshModel<VERTEXTYPE>, IViewPosition3D
        where VERTEXTYPE : struct, IVertexType
    {
        Matrix _modelMatrix = Matrix.Identity;
        public Matrix ModelMatrix
        {
            get { return _modelMatrix; }
            set { _modelMatrix = value; }
        }
        public VERTEXTYPE[] Verticies
        {
            get; set;
        }

        public int[] Edges
        {
            get; set;
        }

        /// <summary>
        /// Specify the expected renderer behavior for this model
        /// </summary>
        public PrimitiveType Primitive { get; set; } = PrimitiveType.TriangleList;

        public int PrimitiveCount
        {
            get
            {
                switch (Primitive)
                {
                    case PrimitiveType.TriangleList:
                        return this.Edges.Length / 3;
                    case PrimitiveType.LineList:
                        return this.Edges.Length / 2;
                    case PrimitiveType.LineStrip:
                        return this.Edges.Length - 1;
                    case PrimitiveType.TriangleStrip:
                        return this.Edges.Length - 2;
                    default:
                        throw new NotImplementedException("Unexpected primitive type");
                }
            }
        }

        static MeshModel()
        {
            VERTEXTYPE v = new VERTEXTYPE();
            VertexElement[] vertex_elements = v.VertexDeclaration.GetVertexElements();
            _HasNormal = vertex_elements.Any(e => e.VertexElementUsage == VertexElementUsage.Normal);
            _HasColor = vertex_elements.Any(e => e.VertexElementUsage == VertexElementUsage.Color);
        }

        //private static readonly VertexElement[] vertex_elements;

        private static readonly bool _HasNormal;
        public bool HasNormal { get => _HasNormal; }

        private static readonly bool _HasColor;
        public bool HasColor { get => _HasColor; }
          
        /// <summary>
        /// Adds the passed verticies to the model, returns index at which first vertex was added
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public int AppendVerticies(ICollection<VERTEXTYPE> input)
        {
            if (Verticies == null)
            {
                Verticies = input.ToArray();
                return 0;
            }

            int iInsert = Verticies.Length;
            
            /////////////////////////
            //Extend our vertex array
            //VERTEXTYPE[] newVerts = new VERTEXTYPE[input.Count + Verticies.Length];
            //Array.Copy(Verticies, newVerts, Verticies.Length);
            Verticies = Verticies.AddRange(input.ToArray());
            /////////////////////////

            //Array.Copy(input.ToArray(), 0, Verticies, iInsert, input.Count);

            return iInsert;
        }

        public void AppendEdges(ICollection<int> newEdges)
        {
            if (Edges == null)
            {
                Edges = newEdges.ToArray();
            }
            else
            {
                //Edges = Edges.Concat(newEdges).ToArray();
                Edges = Edges.AddRange(newEdges.ToArray());
            }
        }

        public GridVector3 Position
        {
            get
            {
                return _modelMatrix.Translation.ToGridVector3();
            }

            set
            {
                _modelMatrix.Translation = value.ToXNAVector3();
            }
        }

        public MeshModel()
        {
        }
    }

    /// <summary>
    /// A helper class that assumes the entire mesh model is the same color
    /// </summary>
    public class PositionColorMeshModel : MeshModel<VertexPositionColor>, IColorView
    { 

        public PositionColorMeshModel()
        {
        }

        public float Alpha
        {
            get
            {
                return Verticies.First().Color.GetAlpha();
            }

            set
            {
                if (value != Alpha)
                {
                    Color newColor = this.Color.SetAlpha(value);
                    for (int i = 0; i < Verticies.Length; i++)
                    {
                        Verticies[i].Color = newColor;
                    }
                }
            }
        }

        public Color Color
        {
            get
            {
                return Verticies.First().Color;
            }

            set
            {
                if(value != Color)
                {
                    for(int i =0; i < Verticies.Length; i++)
                    {
                        Verticies[i].Color = value;
                    }
                }
            }
        }
    }
}
