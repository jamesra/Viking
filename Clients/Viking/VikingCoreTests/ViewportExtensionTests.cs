using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="ViewportExtension.UnprojectEx"/>: it must invert the world/view/projection
    /// transform that maps a point to screen space, honoring the viewport offset, size and depth range.
    /// </summary>
    [TestClass]
    public class ViewportExtensionTests
    {
        private static readonly Viewport TestViewport = new(10, 20, 800, 600) { MinDepth = 0f, MaxDepth = 1f };

        [TestMethod]
        public void ViewportCenter_AtNearPlane_UnprojectsToViewCenter()
        {
            Matrix projection = Matrix.CreateOrthographic(100f, 75f, 0f, 10f);
            Vector3 screen = new(10f + 400f, 20f + 300f, 0f);

            Vector3 world = TestViewport.UnprojectEx(screen, projection, Matrix.Identity, Matrix.Identity);

            Assert.AreEqual(0f, world.X, 1e-4f);
            Assert.AreEqual(0f, world.Y, 1e-4f);
            Assert.AreEqual(0f, world.Z, 1e-4f);
        }

        [TestMethod]
        public void ScreenTopLeft_UnprojectsToNegativeXPositiveY()
        {
            Matrix projection = Matrix.CreateOrthographic(100f, 75f, 0f, 10f);
            Vector3 screen = new(10f, 20f, 0f);

            Vector3 world = TestViewport.UnprojectEx(screen, projection, Matrix.Identity, Matrix.Identity);

            Assert.AreEqual(-50f, world.X, 1e-3f);
            Assert.AreEqual(37.5f, world.Y, 1e-3f);
        }

        [TestMethod]
        public void ViewAndWorldTranslation_AreAppliedInverted()
        {
            Matrix projection = Matrix.CreateOrthographic(100f, 75f, 0f, 10f);
            Matrix view = Matrix.CreateTranslation(-5f, 0f, 0f);
            Matrix worldMatrix = Matrix.CreateScale(2f) * Matrix.CreateTranslation(0f, 2f, 0f);
            Vector3 screen = new(10f + 400f, 20f + 300f, 0f);

            Vector3 world = TestViewport.UnprojectEx(screen, projection, view, worldMatrix);

            // A model point p lands at (2p + (0,2)) + (-5,0); the view center is therefore p = (2.5, -1, 0).
            // The scale makes world and view non-commuting, so swapping their order is detected.
            Assert.AreEqual(2.5f, world.X, 1e-4f);
            Assert.AreEqual(-1f, world.Y, 1e-4f);
        }

        [TestMethod]
        public void Unproject_InvertsProject_ForPerspectiveCamera()
        {
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 800f / 600f, 1f, 1000f);
            Matrix view = Matrix.CreateLookAt(new Vector3(0f, 0f, 20f), Vector3.Zero, Vector3.Up);

            Gen<float> coord = Gen.Choose(-500, 500).Select(i => i / 100f);
            Gen<Vector3> pointGen = from x in coord
                                    from y in coord
                                    from z in coord
                                    select new Vector3(x, y, z);

            Prop.ForAll(Arb.From(pointGen), p =>
            {
                Vector3 screen = TestViewport.Project(p, projection, view, Matrix.Identity);
                Vector3 back = TestViewport.UnprojectEx(screen, projection, view, Matrix.Identity);
                return (back - p).Length() < 5e-2f;
            }).QuickCheckThrowOnFailure();
        }
    }
}
