using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    public class FontRenderData(string fontName) : IInitEffect
    {
        public SpriteFont Font = null;

        public SpriteBatch SpriteBatch = null;

        /// <summary>
        /// Must be set at program start before we request the default font
        /// </summary>
        public readonly string FontName = fontName;

        public void Init(GraphicsDevice device, ContentManager content)
        {
            SpriteBatch = new SpriteBatch(device)
            {
                Name = FontName
            };
            Font = content.Load<SpriteFont>(FontName);
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, this))
                return true;

            if (obj is null)
                return false;

            FontRenderData other = obj as FontRenderData;
            if (obj is null)
                return false;

            return other.FontName == this.FontName;
        }

        public override int GetHashCode() => FontName.GetHashCode();
    }

    public static class DeviceFontStore
    {
        private static readonly Dictionary<GraphicsDevice, Dictionary<string, FontRenderData>> ManagersForDevice = [];

        public static string DefaultFont = @"Arial";

        public static FontRenderData GetOrCreateForDevice(GraphicsDevice device, ContentManager content, string FontName = null)
        {
            // Check if device is disposed
            if (device == null || device.IsDisposed)
                return null;

            FontName ??= DefaultFont;

            if (ManagersForDevice.TryGetValue(device, out Dictionary<string, FontRenderData> fontDict))
            {
                if (fontDict.TryGetValue(FontName, out FontRenderData result))
                {
                    return result;
                }
            }
            else
            {
                fontDict = [];
                ManagersForDevice.Add(device, fontDict);
            }

            FontRenderData fontData = new(FontName);

            fontData.Init(device, content);

            fontDict.Add(FontName, fontData);

            return fontData;
        }

        public static FontRenderData TryGet(GraphicsDevice device, string FontName = null)
        {
            // Check if device is disposed
            if (device == null || device.IsDisposed)
                return null;

            FontName ??= DefaultFont;

            if (ManagersForDevice.TryGetValue(device, out Dictionary<string, FontRenderData> fontDict))
            {
                if (fontDict.TryGetValue(FontName, out FontRenderData result))
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Clear all cached entries for a specific device. Call this when the device is reset.
        /// </summary>
        /// <param name="device">The device to clear entries for, or null to clear all entries</param>
        public static void ClearForDevice(GraphicsDevice device)
        {
            if (device == null)
            {
                ManagersForDevice.Clear();
                return;
            }

            if (ManagersForDevice.ContainsKey(device))
            {
                ManagersForDevice.Remove(device);
            }

            // Also clear any entries for disposed devices
            var disposedDevices = ManagersForDevice.Keys.Where(d => d.IsDisposed).ToList();
            foreach (var disposedDevice in disposedDevices)
            {
                ManagersForDevice.Remove(disposedDevice);
            }
        }

        /// <summary>
        /// Clear all cached entries for all devices. Call this on device reset.
        /// </summary>
        public static void ClearAll()
        {
            ManagersForDevice.Clear();
        }
    }
}
