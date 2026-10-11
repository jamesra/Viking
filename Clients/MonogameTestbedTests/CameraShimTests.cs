using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using VikingXNA;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace MonogameTestbedTests
{
    /// <summary>
    /// The section-correction shim moves everything drawn through the camera while pointer input still maps to true
    /// volume coordinates.
    /// </summary>
    [TestClass]
    public class CameraShimTests
    {
        const int Width = 800;
        const int Height = 600;

        static Scene CreateScene(Vector2 lookAt, Vector2 shim, double downsample)
        {
            Camera camera = new() { LookAt = lookAt, Downsample = downsample };
            camera.Shim = shim;
            return new Scene(new Viewport(0, 0, Width, Height), camera);
        }

        [TestMethod]
        public void ScreenToWorld_ReturnsTrueVolumePosition_NotTheShiftedOne()
        {
            Scene scene = CreateScene(new Vector2(1000, 2000), new Vector2(30, -10), downsample: 2);

            Geometry.Vector2 centre = scene.ScreenToWorld(Width / 2.0, Height / 2.0);

            // The content under the screen centre is the content whose true position is LookAt - Shim.
            Assert.AreEqual(970, centre.X, 1e-3);
            Assert.AreEqual(2010, centre.Y, 1e-3);
        }

        [TestMethod]
        public void WorldToScreen_ShiftsContentByTheShim_AndRoundTripsWithScreenToWorld()
        {
            Vector2 lookAt = new(1000, 2000);
            Scene plain = CreateScene(lookAt, Vector2.Zero, downsample: 2);
            Scene shimmed = CreateScene(lookAt, new Vector2(30, -10), downsample: 2);

            Geometry.Vector2 before = plain.WorldToScreen(1100, 2100);
            Geometry.Vector2 after = shimmed.WorldToScreen(1100, 2100);

            // +30 volume units at 2 units per pixel is 15 pixels right; -10 in Y is 5 pixels down (screen Y is flipped).
            Assert.AreEqual(before.X + 15, after.X, 1e-2);
            Assert.AreEqual(before.Y + 5, after.Y, 1e-2);

            Geometry.Vector2 back = shimmed.ScreenToWorld(after);
            Assert.AreEqual(1100, back.X, 1e-2);
            Assert.AreEqual(2100, back.Y, 1e-2);
        }

        [TestMethod]
        public void VisibleWorldBounds_AreTrueVolumeBounds_AndSettingThemKeepsTheCentre()
        {
            Scene scene = CreateScene(new Vector2(1000, 2000), new Vector2(30, -10), downsample: 2);

            Geometry.Rectangle bounds = scene.VisibleWorldBounds;
            Assert.AreEqual(970, bounds.Center.X, 1e-3);
            Assert.AreEqual(2010, bounds.Center.Y, 1e-3);

            scene.VisibleWorldBounds = new Geometry.Rectangle(new Geometry.Vector2(5000, 6000), 1600, 1200);
            Geometry.Vector2 centre = scene.ScreenToWorld(Width / 2.0, Height / 2.0);
            Assert.AreEqual(5800, centre.X, 1e-2);
            Assert.AreEqual(6600, centre.Y, 1e-2);
        }
    }
}
