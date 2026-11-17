#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GeometryVector2 = Geometry.Vector2;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Draws the proposals' accept buttons (a green circle with a white check) and hide buttons (a red rounded square with a
    /// black X). The shapes and symbols differ as well as the colours, so they can be told apart without colour vision.
    /// Every look is a cell of one shared texture (<see cref="ProposalButtonAtlas"/>), built once per graphics device; each
    /// frame queues one quad per button with <see cref="Add"/> and draws them all in one call with <see cref="Flush"/>.
    /// Render thread only.
    /// </summary>
    internal sealed class ProposalButtonsView
    {
        private static readonly ProposalButtonKind[] Kinds = [ProposalButtonKind.Accept, ProposalButtonKind.Hide];

        /// <summary>The atlas texels, rasterised on first use and kept, so a new or reset device only uploads them.</summary>
        private static readonly Lazy<List<Color[]>> AtlasLevels = new(ProposalButtonAtlas.BuildMipLevels);

        private static readonly object AtlasLock = new();
        private static readonly Dictionary<GraphicsDevice, Texture2D> Atlases = [];

        /// <summary>Quads queued since the last <see cref="Flush"/>; the arrays grow and are reused from frame to frame.</summary>
        private VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[16 * 4];
        private int[] indices = [];
        private int quadCount;

        private BasicEffect? effect;

        /// <summary>
        /// Queues both buttons of <paramref name="placement"/>. <paramref name="phaseOf"/> gives each button's phase,
        /// <paramref name="hovered"/> the one under the cursor, and <paramref name="alpha"/> the overall opacity.
        /// </summary>
        public void Add(
            ProposalButtonPlacement placement,
            Func<ProposalButtonKind, ProposalButtonPhase> phaseOf,
            ProposalButtonKind? hovered,
            float alpha)
        {
            if (alpha <= 0f || placement.Radius <= 0)
                return;

            Color tint = new(1f, 1f, 1f, Math.Min(1f, alpha));
            double halfSize = ProposalButtonAtlas.QuadHalfSize(placement.Radius);
            foreach (ProposalButtonKind kind in Kinds)
            {
                (Vector2 topLeft, Vector2 bottomRight) = ProposalButtonAtlas.UvOf(kind, phaseOf(kind), hovered == kind);
                AddQuad(placement.CenterOf(kind), halfSize, topLeft, bottomRight, tint);
            }
        }

        /// <summary>Draws every queued button in one call and empties the queue.</summary>
        public void Flush(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            if (quadCount == 0)
                return;

            try
            {
                Texture2D? atlas = AtlasFor(graphicsDevice);
                if (atlas is null)
                    return;

                if (effect is null || effect.IsDisposed || effect.GraphicsDevice != graphicsDevice)
                {
                    effect?.Dispose();
                    effect = new BasicEffect(graphicsDevice) { TextureEnabled = true, VertexColorEnabled = true, LightingEnabled = false };
                }

                effect.Texture = atlas;
                effect.World = scene.World;
                effect.View = scene.View;
                effect.Projection = scene.Projection;

                BlendState blend = graphicsDevice.BlendState;
                DepthStencilState depth = graphicsDevice.DepthStencilState;
                RasterizerState rasterizer = graphicsDevice.RasterizerState;
                SamplerState sampler = graphicsDevice.SamplerStates[0];
                try
                {
                    // The atlas is not premultiplied; the buttons sit on top of everything; mipmapped linear filtering keeps
                    // them smooth from the largest size down to the fade.
                    graphicsDevice.BlendState = BlendState.NonPremultiplied;
                    graphicsDevice.DepthStencilState = DepthStencilState.None;
                    graphicsDevice.RasterizerState = RasterizerState.CullNone;
                    graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

                    EnsureIndices(quadCount);
                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, quadCount * 4, indices, 0, quadCount * 2);
                    }
                }
                finally
                {
                    graphicsDevice.BlendState = blend;
                    graphicsDevice.DepthStencilState = depth;
                    graphicsDevice.RasterizerState = rasterizer;
                    graphicsDevice.SamplerStates[0] = sampler;
                }
            }
            finally
            {
                quadCount = 0;
            }
        }

        private void AddQuad(GeometryVector2 center, double halfSize, Vector2 topLeft, Vector2 bottomRight, Color tint)
        {
            if ((quadCount + 1) * 4 > vertices.Length)
                Array.Resize(ref vertices, vertices.Length * 2);

            float left = (float)(center.X - halfSize), right = (float)(center.X + halfSize);
            float top = (float)(center.Y + halfSize), bottom = (float)(center.Y - halfSize);
            int v = quadCount * 4;

            // World y is up and texture rows run down, so the top edge takes the cell's top row.
            vertices[v] = new VertexPositionColorTexture(new Vector3(left, top, 0), tint, topLeft);
            vertices[v + 1] = new VertexPositionColorTexture(new Vector3(right, top, 0), tint, new Vector2(bottomRight.X, topLeft.Y));
            vertices[v + 2] = new VertexPositionColorTexture(new Vector3(left, bottom, 0), tint, new Vector2(topLeft.X, bottomRight.Y));
            vertices[v + 3] = new VertexPositionColorTexture(new Vector3(right, bottom, 0), tint, bottomRight);
            quadCount++;
        }

        /// <summary>Two triangles per quad; built for the largest batch so far and reused.</summary>
        private void EnsureIndices(int quads)
        {
            if (indices.Length >= quads * 6)
                return;

            int capacity = Math.Max(quads, vertices.Length / 4);
            indices = new int[capacity * 6];
            for (int q = 0; q < capacity; q++)
            {
                int v = q * 4, i = q * 6;
                indices[i] = v + 2;
                indices[i + 1] = v + 1;
                indices[i + 2] = v;
                indices[i + 3] = v + 3;
                indices[i + 4] = v + 1;
                indices[i + 5] = v + 2;
            }
        }

        /// <summary>The atlas on <paramref name="device"/>, uploaded on first use and again after the device drops it.</summary>
        private static Texture2D? AtlasFor(GraphicsDevice device)
        {
            if (device is null || device.IsDisposed)
                return null;

            lock (AtlasLock)
            {
                if (Atlases.TryGetValue(device, out Texture2D? cached) && !cached.IsDisposed)
                    return cached;

                // Drop textures of devices that are gone.
                List<GraphicsDevice> stale = [];
                foreach (KeyValuePair<GraphicsDevice, Texture2D> entry in Atlases)
                {
                    if (entry.Key.IsDisposed || entry.Value.IsDisposed)
                        stale.Add(entry.Key);
                }

                foreach (GraphicsDevice gone in stale)
                    Atlases.Remove(gone);

                Texture2D atlas = new(device, ProposalButtonAtlas.Width, ProposalButtonAtlas.Height, true, SurfaceFormat.Color);
                List<Color[]> levels = AtlasLevels.Value;
                for (int level = 0; level < atlas.LevelCount && level < levels.Count; level++)
                    atlas.SetData(level, null, levels[level], 0, levels[level].Length);

                Atlases[device] = atlas;
                return atlas;
            }
        }
    }
}
