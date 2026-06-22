using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
            get => _modelMatrix;
            set => _modelMatrix = value;
        }

        private VERTEXTYPE[] _verticies;
        public VERTEXTYPE[] Verticies
        {
            get => _verticies;
            set { _verticies = value; _bufferDirty = true; }
        }

        private int[] _edges;
        public int[] Edges
        {
            get => _edges;
            set { _edges = value; _bufferDirty = true; }
        }

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private bool _bufferDirty = true;

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
        /// Marks the cached vertex/index buffers as invalid (e.g. after in-place vertex changes). Subclasses may call this.
        /// </summary>
        protected void InvalidateBuffers()
        {
            _bufferDirty = true;
        }

        /// <summary>
        /// Ensures vertex and index buffers are created and up to date for the given device. Call before drawing.
        /// </summary>
        /// <returns>True if buffers are valid and can be used for DrawIndexedPrimitives; false if geometry is empty or invalid.</returns>
        public bool EnsureBuffers(GraphicsDevice device)
        {
            if (device == null || _verticies == null || _verticies.Length == 0 || _edges == null || _edges.Length == 0)
                return false;

            if (!_bufferDirty && _vertexBuffer != null && !_vertexBuffer.IsDisposed && _indexBuffer != null && !_indexBuffer.IsDisposed &&
                _vertexBuffer.VertexCount == _verticies.Length && _indexBuffer.IndexCount == _edges.Length)
                return true;

            _vertexBuffer?.Dispose();
            _vertexBuffer = null;
            _indexBuffer?.Dispose();
            _indexBuffer = null;

            VERTEXTYPE v = _verticies[0];
            int declStride = v.VertexDeclaration?.VertexStride ?? -1;
            int vertexCount = _verticies.Length;
            int marshalSize = Marshal.SizeOf<VERTEXTYPE>();
            _vertexBuffer = new VertexBuffer(device, typeof(VERTEXTYPE), _verticies.Length, BufferUsage.None);
            _vertexBuffer.SetData(0, _verticies, 0, _verticies.Length, marshalSize);

            _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, _edges.Length, BufferUsage.None);
            _indexBuffer.SetData(_edges, 0, _edges.Length);

            _bufferDirty = false;
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

        public GridVector3 Position
        {
            get => _modelMatrix.Translation.ToGridVector3();

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
