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
        private static Dictionary<GraphicsDevice, T> ManagersForDevice = new Dictionary<GraphicsDevice, T>();
        
        public static T GetOrCreateForDevice(GraphicsDevice device, ContentManager content)
        {
            if (ManagersForDevice.ContainsKey(device))
            {
                return ManagersForDevice[device];
            }

            T manager = new T();
            manager.Init(device, content);

            ManagersForDevice[device] = manager;

            //device.DeviceLost += OnDeviceLostOrReset;
            //device.DeviceResetting += OnDeviceLostOrReset;
            return manager;
        }

        public static T TryGet(GraphicsDevice device)
        {
            if (!ManagersForDevice.ContainsKey(device))
                return null;

            return ManagersForDevice[device];
        }

        /*
        private static void OnDeviceLostOrReset(object sender, EventArgs e)
        {
            GraphicsDevice device = sender as GraphicsDevice;
            if (device != null)
            {
                if (ManagersForDevice.ContainsKey(device))
                {
                    ManagersForDevice.Remove(device);
                    device.DeviceReset -= OnDeviceLostOrReset;
                    device.DeviceLost -= OnDeviceLostOrReset;
                }
            }
        }*/
    }
}
