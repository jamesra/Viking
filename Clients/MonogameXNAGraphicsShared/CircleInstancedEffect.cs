using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Effect and mesh for drawing many circles (solid and textured) in a single instanced draw call.
    /// </summary>
    public class CircleInstancedEffect : IInitEffect
    {
        public const int MaxInstancesPerBatch = 200;

        private GraphicsDevice _device;
        private Effect _effect;
        private EffectParameter _viewProj;
        private EffectParameter _circleData;
        private EffectParameter _circleColors;
        private EffectParameter _circleTextures;
        private EffectParameter _invCircleTextureLayers;
        private EffectParameter _renderTargetSize;
        private EffectParameter _backgroundTexture;
        private EffectParameter _inputLumaAlpha;

        private VertexBuffer _vb;
        private IndexBuffer _ib;
        private VertexDeclaration _vdecl;
        private int _numVertices;
        private int _numIndices;

        private Texture2D _circleTextureAtlas;
        private static readonly object _atlasLock = new();

        public Effect Effect => _effect;

        private struct CircleInstanceVertex
        {
            public Vector3 Pos;
            public float Index;

            public static readonly VertexElement[] VertexElements =
            [
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0),
            ];
        }

        public void Init(GraphicsDevice device, ContentManager content)
        {
            _device = device;
            _effect = content.Load<Effect>("CircleInstanced");
            _viewProj = _effect.Parameters["viewProj"];
            _circleData = _effect.Parameters["CircleData"];
            _circleColors = _effect.Parameters["CircleColors"];
            _circleTextures = _effect.Parameters["CircleTextures"];
            _invCircleTextureLayers = _effect.Parameters["InvCircleTextureLayers"];
            _renderTargetSize = _effect.Parameters["RenderTargetSize"];
            _backgroundTexture = _effect.Parameters["BackgroundTexture"];
            _inputLumaAlpha = _effect.Parameters["InputLumaAlpha"];
            CreateCircleInstanceMesh();
        }

        private void CreateCircleInstanceMesh()
        {
            const int vertsPerQuad = 4;
            const int indicesPerQuad = 6;
            _numVertices = MaxInstancesPerBatch * vertsPerQuad;
            _numIndices = MaxInstancesPerBatch * indicesPerQuad;

            var verts = new CircleInstanceVertex[_numVertices];
            var indices = new short[_numIndices];

            Vector3[] quadCorners = [new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(-1, -1, 0), new Vector3(1, -1, 0)];
            int[] quadIndices = [0, 1, 2, 2, 1, 3];

            for (int instance = 0; instance < MaxInstancesPerBatch; instance++)
            {
                int vBase = instance * vertsPerQuad;
                int iBase = instance * indicesPerQuad;
                for (int v = 0; v < vertsPerQuad; v++)
                {
                    verts[vBase + v] = new CircleInstanceVertex { Pos = quadCorners[v], Index = instance };
                }
                for (int i = 0; i < indicesPerQuad; i++)
                {
                    indices[iBase + i] = (short)(vBase + quadIndices[i]);
                }
            }

            _vdecl = new VertexDeclaration(CircleInstanceVertex.VertexElements);
            _vb = new VertexBuffer(_device, _vdecl, _numVertices, BufferUsage.None);
            _vb.SetData(verts);
            _ib = new IndexBuffer(_device, IndexElementSize.SixteenBits, _numIndices, BufferUsage.None);
            _ib.SetData(indices);
        }

        /// <summary>
        /// Build a 2D texture atlas with one layer per BuiltinTexture (layers stacked vertically).
        /// Layer 0 = solid white for untextured circles.
        /// </summary>
        public static Texture2D BuildCircleTextureAtlas(GraphicsDevice device)
        {
            lock (_atlasLock)
            {
                var values = (BuiltinTexture[])Enum.GetValues(typeof(BuiltinTexture));
                int numLayers = values.Length;
                int layerWidth = 64;
                int layerHeight = 64;

                Texture2D atlas = new Texture2D(device, layerWidth, layerHeight * numLayers);
                Color[] white = [Color.White];
                var layerPixels = new Color[layerWidth * layerHeight];

                for (int layer = 0; layer < numLayers; layer++)
                {
                    Texture2D src = values[layer].GetTexture();
                    if (src == null || src.IsDisposed)
                    {
                        for (int i = 0; i < layerPixels.Length; i++)
                            layerPixels[i] = Color.White;
                    }
                    else
                    {
                        int w = Math.Min(src.Width, layerWidth);
                        int h = Math.Min(src.Height, layerHeight);
                        var srcData = new Color[src.Width * src.Height];
                        src.GetData(srcData);
                        for (int y = 0; y < layerHeight; y++)
                        {
                            for (int x = 0; x < layerWidth; x++)
                            {
                                int idx = y * layerWidth + x;
                                if (x < w && y < h)
                                    layerPixels[idx] = srcData[y * src.Width + x];
                                else
                                    layerPixels[idx] = Color.Transparent;
                            }
                        }
                    }

                    atlas.SetData(0, new Rectangle(0, layer * layerHeight, layerWidth, layerHeight), layerPixels, 0, layerPixels.Length);
                }

                return atlas;
            }
        }

        private void EnsureCircleTextureAtlas()
        {
            if (_circleTextureAtlas != null && !_circleTextureAtlas.IsDisposed)
                return;
            _circleTextureAtlas = BuildCircleTextureAtlas(_device);
        }

        public void SetTechnique(OverlayStyle style)
        {
            _effect.CurrentTechnique = style == OverlayStyle.Alpha
                ? _effect.Techniques["CircleInstancedAlpha"]
                : _effect.Techniques["CircleInstancedLuma"];
        }

        public void Draw(
            Matrix viewProj,
            Vector2 renderTargetSize,
            Texture2D backgroundTexture,
            float inputLumaAlpha,
            Vector4[] circleData,
            Vector4[] circleColors,
            int count)
        {
            if (count <= 0)
                return;

            EnsureCircleTextureAtlas();
            _viewProj?.SetValue(viewProj);
            _circleData?.SetValue(circleData);
            _circleColors?.SetValue(circleColors);
            _circleTextures?.SetValue(_circleTextureAtlas);
            _invCircleTextureLayers?.SetValue(1f / Enum.GetValues(typeof(BuiltinTexture)).Length);
            _renderTargetSize?.SetValue(renderTargetSize);
            _backgroundTexture?.SetValue(backgroundTexture);
            _inputLumaAlpha?.SetValue(inputLumaAlpha);

            _device.SetVertexBuffer(_vb);
            _device.Indices = _ib;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, count * 2);
            }

            _device.SetVertexBuffer(null);
            _device.Indices = null;
        }
    }
}
