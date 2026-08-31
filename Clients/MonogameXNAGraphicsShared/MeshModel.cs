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
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    public interface IMeshModel<VERTEXTYPE>
         where VERTEXTYPE : struct, IVertexType
    {
        VERTEXTYPE[] Vertices { get; }
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
        public VERTEXTYPE[] Vertices
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

        /// <summary>
        /// Primitives to draw. Reports what the index buffer actually holds when one has been uploaded, because
        /// <see cref="Edges"/> can be replaced by a meshing thread between EnsureBuffers and the draw call.
        /// </summary>
        public int PrimitiveCount
        {
            get
            {
                int indexCount = _indexBuffer != null && !_indexBuffer.IsDisposed
                    ? _indexBuffer.IndexCount
                    : this.Edges?.Length ?? 0;

                return Primitive switch
                {
                    PrimitiveType.TriangleList => indexCount / 3,
                    PrimitiveType.LineList => indexCount / 2,
                    PrimitiveType.LineStrip => indexCount - 1,
                    PrimitiveType.TriangleStrip => indexCount - 2,
                    _ => throw new NotImplementedException("Unexpected primitive type"),
                };
            }
        }

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
        /// Marks the cached vertex/index buffers as invalid. Callers that mutate the contents of
        /// <see cref="Vertices"/> or <see cref="Edges"/> in place must call this, because only the property
        /// setters detect assignment and an already-uploaded buffer is otherwise never refreshed.
        /// </summary>
        public void InvalidateBuffers()
        {
            _bufferDirty = true;
        }

        /// <summary>
        /// Ensures vertex and index buffers are created and up to date for the given device. Call before drawing.
        /// </summary>
        /// <returns>True if buffers are valid and can be used for DrawIndexedPrimitives; false if geometry is empty or invalid.</returns>
        public bool EnsureBuffers(GraphicsDevice device)
        {
            if (device == null)
                return false;

            //Clear the flag before reading the arrays. A writer that swaps them after this point sets it again
            //and the next frame rebuilds; clearing at the end would swallow that and leave a stale buffer.
            bool wasDirty = _bufferDirty;
            _bufferDirty = false;

            //These models are filled from meshing threads while the draw thread renders them, and the setters
            //replace the whole array rather than mutating in place. Snapshot both references so the lengths used
            //to size a buffer cannot change before the buffer is filled.
            VERTEXTYPE[] verticies = _verticies;
            int[] edges = _edges;

            if (verticies == null || verticies.Length == 0 || edges == null || edges.Length == 0)
                return false;

            if (!wasDirty && _vertexBuffer != null && !_vertexBuffer.IsDisposed && _indexBuffer != null && !_indexBuffer.IsDisposed &&
                _vertexBuffer.VertexCount == verticies.Length && _indexBuffer.IndexCount == edges.Length)
                return true;

            //Verticies and edges are published independently, so a partially applied update can leave indices
            //pointing past the end of the vertex array. Wait for the matching vertices rather than drawing it.
            //Validating below the not-dirty early-out keeps a fully assembled model off this O(edges) scan every
            //frame: the existing buffers can only have been uploaded by a previous pass through this check, and
            //any array swap since then set _bufferDirty, which forces this path and revalidates. Re-setting
            //_bufferDirty here (rather than relying on the clear at the top) is what makes the next frame retry.
            for (int i = 0; i < edges.Length; i++)
            {
                if ((uint)edges[i] >= (uint)verticies.Length)
                {
                    _bufferDirty = true;
                    return false;
                }
            }

            _vertexBuffer?.Dispose();
            _vertexBuffer = null;
            _indexBuffer?.Dispose();
            _indexBuffer = null;

            int marshalSize = Marshal.SizeOf<VERTEXTYPE>();
            _vertexBuffer = new VertexBuffer(device, typeof(VERTEXTYPE), verticies.Length, BufferUsage.None);
            _vertexBuffer.SetData(0, verticies, 0, verticies.Length, marshalSize);

            _indexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, edges.Length, BufferUsage.None);
            _indexBuffer.SetData(edges, 0, edges.Length);

            return true;
        }

        /// <summary>
        /// Adds the passed verticies to the model, returns index at which first vertex was added
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public int AppendVerticies(ICollection<VERTEXTYPE> input)
        {
            VERTEXTYPE[] existing = _verticies;
            int iInsert = existing?.Length ?? 0;

            Vertices = Appended(existing, input);

            return iInsert;
        }

        public void AppendEdges(ICollection<int> newEdges)
        {
            Edges = Appended(_edges, newEdges);
        }

        /// <summary>
        /// Concatenation into a freshly allocated array, sized once and filled by copying both sides directly.
        /// </summary>
        /// <remarks>
        /// The result is only ever handed to the property setters, so the array the draw thread is currently
        /// reading is never touched: readers see either the old array or the fully built new one.
        /// </remarks>
        private static T[] Appended<T>(T[] existing, ICollection<T> addition)
        {
            int existingCount = existing?.Length ?? 0;
            int additionCount = addition.Count;

            T[] result = new T[existingCount + additionCount];

            if (existingCount > 0)
                Array.Copy(existing, result, existingCount);

            if (additionCount > 0)
                addition.CopyTo(result, existingCount);

            return result;
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
            get => Vertices.First().Color.GetAlpha();

            set
            {
                if (value != Alpha)
                {
                    Color newColor = this.Color.SetAlpha(value);
                    for (int i = 0; i < Vertices.Length; i++)
                    {
                        Vertices[i].Color = newColor;
                    }
                    InvalidateBuffers();
                }
            }
        }

        public Color Color
        {
            get => Vertices.First().Color;

            set
            {
                if (value != Color)
                {
                    for (int i = 0; i < Vertices.Length; i++)
                    {
                        Vertices[i].Color = value;
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
            get => Vertices.First().Color.GetAlpha();

            set
            {
                if (value != Alpha)
                {
                    Color newColor = this.Color.SetAlpha(value);
                    for (int i = 0; i < Vertices.Length; i++)
                    {
                        Vertices[i].Color = newColor;
                    }
                    InvalidateBuffers();
                }
            }
        }

        public Color Color
        {
            get => Vertices.First().Color;

            set
            {
                if (value != Color)
                {
                    for (int i = 0; i < Vertices.Length; i++)
                    {
                        Vertices[i].Color = value;
                    }
                    InvalidateBuffers();
                }
            }
        }
    }
}
