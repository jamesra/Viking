using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace VikingXNAGraphics
{
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

            T curveManager = new T();
            curveManager.Init(device, content);

            ManagersForDevice[device] = curveManager;

            //device.DeviceLost += OnDeviceLostOrReset;
            //device.DeviceResetting += OnDeviceLostOrReset;
            return curveManager;
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
