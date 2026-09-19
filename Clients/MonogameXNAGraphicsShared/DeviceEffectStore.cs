using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Associates objects of a given type with a GraphicsDevice so they can be accessed from across the app in a consistent way
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class DeviceEffectsStore<T> where
        T : class, IInitEffect, new()
    {
        private static readonly Dictionary<GraphicsDevice, T> ManagersForDevice = [];

        public static T GetOrCreateForDevice(GraphicsDevice device, ContentManager content)
        {
            // Check if device is disposed and clear stale entries
            if (device == null || device.IsDisposed)
                return null;

            if (ManagersForDevice.TryGetValue(device, out var entry))
            {
                return entry;
            }

            T manager = new();
            manager.Init(device, content);

            ManagersForDevice[device] = manager;

            return manager;
        }

        public static T TryGet(GraphicsDevice device)
        {
            if (device == null || device.IsDisposed)
                return null;

            return ManagersForDevice.TryGetValue(device, out var entry) ? entry : null;
        }

        /// <summary>
        /// Clear all cached entries for a specific device. Call this when the device is reset.
        /// </summary>
        /// <param name="device">The device to clear entries for, or null to clear all entries</param>
        public static void ClearForDevice(GraphicsDevice device)
        {
            if (device == null)
            {
                DisposeAll();
                ManagersForDevice.Clear();
                return;
            }

            if (ManagersForDevice.TryGetValue(device, out var manager))
            {
                manager.Dispose();
                ManagersForDevice.Remove(device);
            }

            var disposedDevices = ManagersForDevice.Keys.Where(d => d.IsDisposed).ToList();
            foreach (var disposedDevice in disposedDevices)
            {
                ManagersForDevice[disposedDevice].Dispose();
                ManagersForDevice.Remove(disposedDevice);
            }
        }

        /// <summary>
        /// Clear all cached entries for all devices. Call this on device reset.
        /// </summary>
        public static void ClearAll()
        {
            DisposeAll();
            ManagersForDevice.Clear();
        }

        private static void DisposeAll()
        {
            foreach (var manager in ManagersForDevice.Values)
                manager.Dispose();
        }
    }
}
