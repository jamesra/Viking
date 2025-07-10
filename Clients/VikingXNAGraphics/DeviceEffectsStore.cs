using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace VikingXNAGraphics
{
    public static class DeviceEffectsStore<T> where T : IInitEffect, new()
    {
        private static readonly Dictionary<GraphicsDevice, T> _effects = new Dictionary<GraphicsDevice, T>();

        public static T GetOrCreateForDevice(GraphicsDevice device, ContentManager content)
        {
            if (!_effects.TryGetValue(device, out T effect))
            {
                effect = new T();
                effect.Init(device, content);
                _effects[device] = effect;
            }
            return effect;
        }
    }
} 