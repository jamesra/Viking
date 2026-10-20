using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAWinForms;

namespace VikingCoreTests
{
    /// <summary>
    /// Creates the viewer's graphics device the way <see cref="GraphicsDeviceControl"/> does, on a window that is never
    /// shown, and loads every compiled content file Viking ships. Catches a framework build whose MonoGame cannot create a
    /// device for a WinForms window or cannot read the content (MonoGame 3.8 rejects effects compiled for 3.7).
    /// Inconclusive on a machine without a Direct3D adapter.
    /// </summary>
    [TestClass]
    [TestCategory("GPU")]
    public class GraphicsDeviceSmokeTests
    {
        private static readonly string ContentDir = Path.Combine(AppContext.BaseDirectory, "Content");

        [TestMethod]
        public void DeviceIsCreatedAndEveryContentFileLoads()
        {
            string[] files = Directory.Exists(ContentDir)
                ? Directory.GetFiles(ContentDir, "*.xnb").Select(Path.GetFileNameWithoutExtension).ToArray()!
                : [];
            Assert.IsTrue(files.Length > 0, $"No compiled content in {ContentDir}.");

            using Form window = new() { ShowInTaskbar = false, Width = 64, Height = 64 };
            GraphicsDeviceService service;
            try
            {
                service = GraphicsDeviceService.AddRef(window.Handle, 64, 64)!;
            }
            catch (Exception e) when (e is not AssertFailedException)
            {
                Assert.Inconclusive("No graphics device on this machine: " + e.GetBaseException());
                return;
            }

            try
            {
                Assert.IsNotNull(service.GraphicsDevice);
                Assert.IsFalse(service.GraphicsDevice.IsDisposed);

                List<string> failures = [];
                foreach (string name in files)
                {
                    if (!TryLoad(service, name, out string error))
                        failures.Add($"{name}: {error}");
                }

                Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
            }
            finally
            {
                service.Release(true);
            }
        }

        /// <summary>Loads <paramref name="name"/> as an effect, a texture or a sprite font, whichever it was compiled as.</summary>
        private static bool TryLoad(GraphicsDeviceService service, string name, out string error)
        {
            error = "";
            foreach (Func<ContentManager, object> load in new Func<ContentManager, object>[]
                     {
                         content => content.Load<Effect>(name),
                         content => content.Load<Texture2D>(name),
                         content => content.Load<SpriteFont>(name),
                     })
            {
                ContentManager content = new(service.Content.ServiceProvider, ContentDir);
                try
                {
                    object loaded = load(content);
                    if (loaded is not null)
                        return true;
                }
                catch (Exception e)
                {
                    error = e.GetBaseException().Message;
                }
                finally
                {
                    content.Unload();
                }
            }

            return false;
        }
    }
}
