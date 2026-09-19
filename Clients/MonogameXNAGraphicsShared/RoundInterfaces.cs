using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Per-device GPU effect that DeviceEffectsStore constructs and tears down on reset.
    /// Dispose must release VertexBuffer/IndexBuffer/VertexDeclaration owned by the manager.
    /// Content-loaded Effect instances stay with ContentManager.
    /// </summary>
    public interface IInitEffect : IDisposable
    {
        void Init(GraphicsDevice device, ContentManager content);
    }

    /// <summary>
    /// Shared GPU buffer teardown for RoundLine and RoundCurve managers.
    /// Does not merge shaders; only the Init/Dispose lifetime is shared.
    /// </summary>
    public static class EffectManagerLifetime
    {
        /// <summary>
        /// Disposes manager-owned mesh buffers. Safe to call when some references are already null.
        /// </summary>
        public static void DisposeOwnedBuffers(ref VertexBuffer vb, ref IndexBuffer ib, ref VertexDeclaration vdecl)
        {
            if (vb != null && !vb.IsDisposed)
                vb.Dispose();
            if (ib != null && !ib.IsDisposed)
                ib.Dispose();
            if (vdecl != null && !vdecl.IsDisposed)
                vdecl.Dispose();
            vb = null;
            ib = null;
            vdecl = null;
        }
    }
}
