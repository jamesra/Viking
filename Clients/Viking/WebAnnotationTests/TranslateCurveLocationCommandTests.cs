using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using VikingXNAGraphics;
using WebAnnotation;
using WebAnnotation.UI.Commands;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="TranslateCurveLocationCommand.PickNearest"/>, which Ctrl-copy of control points uses to choose
    /// the open curve under the mouse from the R-tree hits (listed in no particular order).
    /// </summary>
    [TestClass]
    public class TranslateCurveLocationCommandTests
    {
        /// <summary>Stand-in hit object; only identity matters.</summary>
        private sealed class FakeHit : IHitTesting
        {
            public Geometry.Rectangle BoundingBox => default;
            public bool Contains(Geometry.Vector2 Position) => false;
        }

        private static HitTestResult Hit(FakeHit obj, double distance) => new(obj, 0, 0, distance);

        /// <summary>The farther curve is listed first; the nearer one must still win.</summary>
        [TestMethod]
        public void PickNearest_FartherListedFirst_ReturnsNearer()
        {
            FakeHit far = new(), near = new();
            HitTestResult result = TranslateCurveLocationCommand.PickNearest([Hit(far, 9.0), Hit(near, 2.0)]);
            Assert.AreSame(near, result.obj);
        }

        /// <summary>A hit with distance 0 (point inside) beats a positive distance regardless of order.</summary>
        [TestMethod]
        public void PickNearest_NearerListedFirst_ReturnsNearer()
        {
            FakeHit far = new(), near = new();
            HitTestResult result = TranslateCurveLocationCommand.PickNearest([Hit(near, 0.0), Hit(far, 5.0)]);
            Assert.AreSame(near, result.obj);
        }

        /// <summary>Null (the section has no annotations) and empty lists give null instead of throwing.</summary>
        [TestMethod]
        public void PickNearest_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(TranslateCurveLocationCommand.PickNearest(null));
            Assert.IsNull(TranslateCurveLocationCommand.PickNearest(new List<HitTestResult>()));
        }
    }
}
