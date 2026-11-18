using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    /// <summary>
    /// A vertex array and an index array (<see cref="Edges"/>) with a model matrix, drawn as <see cref="Primitive"/>.
    /// GPU buffers are built lazily. A buffer is created again only when its element count or device changes; otherwise new
    /// vertex or index data (a colour or alpha change, a replaced array of the same length) is uploaded into the one it has.
    /// </summary>
    /// <typeparam name="VERTEXTYPE">Vertex layout; normal and color support are read from its declaration.</typeparam>
    public class MeshModel<VERTEXTYPE> : IViewPosition3D
        where VERTEXTYPE : struct, IVertexType
    {
        Matrix _modelMatrix = Matrix.Identity;
        public Matrix ModelMatrix
        {
            get => _modelMatrix;
            set => _modelMatrix = value;
        }

        private VERTEXTYPE[] _verticies;
        public VERTEXTYPE[] Verticies
        {
            get => _verticies;
            set { _verticies = value; _verticesDirty = true; }
        }

        private int[] _edges;
        public int[] Edges
        {
            get => _edges;
            set { _edges = value; _indicesDirty = true; }
        }

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private bool _verticesDirty = true;
        private bool _indicesDirty = true;

        /// <summary>
        /// Vertex buffer for drawing. Valid after EnsureBuffers(device) returns true.
        /// </summary>
        public VertexBuffer VertexBuffer => _vertexBuffer;

        /// <summary>
        /// Index buffer for drawing. Valid after EnsureBuffers(device) returns true.
        /// </summary>
        public IndexBuffer IndexBuffer => _indexBuffer;

        /// <summary>
        /// Specify the expected renderer behavior for this model
        /// </summary>
        public PrimitiveType Primitive { get; set; } = PrimitiveType.TriangleList;

        public int PrimitiveCount => Primitive switch
        {
            PrimitiveType.TriangleList => this.Edges.Length / 3,
            PrimitiveType.LineList => this.Edges.Length / 2,
            PrimitiveType.LineStrip => this.Edges.Length - 1,
            PrimitiveType.TriangleStrip => this.Edges.Length - 2,
            _ => throw new NotImplementedException("Unexpected primitive type"),
        };

        static MeshModel()
        {
            VERTEXTYPE v = new();
            VertexElement[] vertex_elements = v.VertexDeclaration.GetVertexElements();
            _HasNormal = vertex_elements.Any(e => e.VertexElementUsage == VertexElementUsage.Normal);
            _HasColor = vertex_elements.Any(e => e.VertexElementUsage == VertexElementUsage.Color);
        }

        //private static readonly VertexElement[] vertex_elements;

        private static readonly bool _HasNormal;
        public bool HasNormal => _HasNormal;

        private static readonly bool _HasColor;
        public bool HasColor => _HasColor;

        /// <summary>
        /// Marks the vertex data as changed in place (a colour or alpha change); the next <see cref="EnsureBuffers"/> uploads it
        /// into the existing vertex buffer. Subclasses may call this.
        /// </summary>
        protected void InvalidateBuffers()
        {
            _verticesDirty = true;
        }

        /// <summary>
        /// Ensures vertex and index buffers are created and up to date for the given device. Call before drawing.
        /// </summary>
        /// <returns>True if buffers are valid and can be used for DrawIndexedPrimitives; false if geometry is empty or invalid.</returns>
        public bool EnsureBuffers(GraphicsDevice device)
        {
            if (device == null || _verticies == null || _verticies.Length == 0 || _edges == null || _edges.Length == 0)
                return false;

            if (_vertexBuffer is null || _vertexBuffer.IsDisposed || _vertexBuffer.GraphicsDevice != device || _vertexBuffer.VertexCount != _verticies.Length)
            {
                _vertexBuffer?.Dispose();
                _vertexBuffer = new VertexBuffer(device, _verticies[0].VertexDeclaration, _verticies.Length, BufferUsage.None);
                _verticesDirty = true;
            }

            if (_verticesDirty)
            {
                _vertexBuffer.SetData(_verticies, 0, _verticies.Length);
                _verticesDirty = false;
            }

            if (_indexBuffer is null || _indexBuffer.IsDisposed || _indexBuffer.GraphicsDevice != device || _indexBuffer.IndexCount != _edges.Length)
            {
                _indexBuffer?.Dispose();
                _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, _edges.Length, BufferUsage.None);
                _indicesDirty = true;
            }

            if (_indicesDirty)
            {
                _indexBuffer.SetData(_edges, 0, _edges.Length);
                _indicesDirty = false;
            }

            return true;
        }

        /// <summary>
        /// Adds the passed verticies to the model, returns index at which first vertex was added
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public int AppendVerticies(ICollection<VERTEXTYPE> input)
        {
            if (Verticies is null)
            {
                Verticies = [.. input];
                return 0;
            }

            int iInsert = Verticies.Length;

            /////////////////////////
            //Extend our vertex array
            //VERTEXTYPE[] newVerts = new VERTEXTYPE[input.Count + Verticies.Length];
            //Array.Copy(Verticies, newVerts, Verticies.Length);
            Verticies = Verticies.AddRange([.. input]);
            /////////////////////////

            //Array.Copy(input.ToArray(), 0, Verticies, iInsert, input.Count);

            return iInsert;
        }

        public void AppendEdges(ICollection<int> newEdges)
        {
            if (Edges is null)
            {
                Edges = [.. newEdges];
            }
            else
            {
                //Edges = Edges.Concat(newEdges).ToArray();
                Edges = Edges.AddRange([.. newEdges]);
            }
        }

        public Geometry.Vector3 Position
        {
            get => _modelMatrix.Translation.ToVector3();

            set => _modelMatrix.Translation = value.ToXNAVector3();
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
            get => Verticies.First().Color.GetAlpha();

            set
            {
                if (value != Alpha)
                {
                    Color newColor = this.Color.SetAlpha(value);
                    for (int i = 0; i < Verticies.Length; i++)
                    {
                        Verticies[i].Color = newColor;
                    }
                    InvalidateBuffers();
                }
            }
        }

        public Color Color
        {
            get => Verticies.First().Color;

            set
            {
                if (value != Color)
                {
                    for (int i = 0; i < Verticies.Length; i++)
                    {
                        Verticies[i].Color = value;
                    }
                    InvalidateBuffers();
                }
            }
        }
    }

    /// <summary>
    /// A helper class that assumes the entire mesh model is the same color
    /// </summary>
    public class PositionColorNormalMeshModel : MeshModel<VertexPositionNormalColor>, IColorView
    {

        public PositionColorNormalMeshModel()
        {
        }

        public float Alpha
        {
            get => Verticies.First().Color.GetAlpha();

            set
            {
                if (value != Alpha)
                {
                    Color newColor = this.Color.SetAlpha(value);
                    for (int i = 0; i < Verticies.Length; i++)
                    {
                        Verticies[i].Color = newColor;
                    }
                    InvalidateBuffers();
                }
            }
        }

        public Color Color
        {
            get => Verticies.First().Color;

            set
            {
                if (value != Color)
                {
                    for (int i = 0; i < Verticies.Length; i++)
                    {
                        Verticies[i].Color = value;
                    }
                    InvalidateBuffers();
                }
            }
        }
    }
}
