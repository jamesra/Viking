using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Xna.Framework.Graphics;

namespace VikingCoreTests
{
    /// <summary>
    /// A <see cref="GraphicsDevice"/> that was never initialized, for tests of the texture pipeline that need a non-null
    /// device but never draw. Creating a texture on it fails, so loads complete with a null texture.
    /// </summary>
    internal static class PlaceholderGraphicsDevice
    {
        /// <summary>
        /// Builds the placeholder. MonoGame 3.8 (.NET 10) locks a device field when a texture made for the device is
        /// finalized, so the lock and resource-list fields get empty instances; left null, that finalizer throws and
        /// ends the test host.
        /// </summary>
        public static GraphicsDevice Create()
        {
            var device = (GraphicsDevice)FormatterServices.GetUninitializedObject(typeof(GraphicsDevice));
            GC.SuppressFinalize(device);

            // MonoGame 3.8 keeps _resourcesLock (object) and _resources (HashSet<WeakReference>); 3.7 uses a List.
            foreach (FieldInfo field in typeof(GraphicsDevice).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.GetValue(device) != null)
                    continue;

                if (field.FieldType == typeof(object))
                    field.SetValue(device, new object());
                else if (field.FieldType == typeof(List<WeakReference>))
                    field.SetValue(device, new List<WeakReference>());
                else if (field.FieldType == typeof(HashSet<WeakReference>))
                    field.SetValue(device, new HashSet<WeakReference>());
            }

            return device;
        }
    }
}
