using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using WebAnnotation.Actions;

namespace WebAnnotationTests.Actions
{
    /// <summary>
    /// The hover effect halves the alpha of the annotation under the mouse and puts it back when the mouse leaves. An
    /// opacity set while it hovers (Annotation Preferences OK) must survive the mouse leaving.
    /// </summary>
    [TestClass]
    public class MouseOverEffectTests
    {
        /// <summary>Stores its alpha in an XNA color, as the annotation views do, so it is rounded to 1/255.</summary>
        private sealed class ColoredView : IColorView
        {
            public Color Color { get; set; } = new Color(200, 100, 50, 128);

            public float Alpha
            {
                get => Color.A / 255f;
                set => Color = new Color(Color.R, Color.G, Color.B, (byte)System.Math.Round(value * 255f));
            }
        }

        [TestMethod]
        public void LeavingAViewRestoresItsAlpha()
        {
            ColoredView view = new();
            float before = view.Alpha;
            MouseOverLocationCanvasViewEffect effect = new() { viewObj = view };

            Assert.AreEqual(before / 2, view.Alpha, 1 / 255f, "hovering halves the alpha");

            effect.viewObj = null;

            Assert.AreEqual(before, view.Alpha, 1e-6);
        }

        [TestMethod]
        public void AnOpacitySetWhileHoveringSurvivesLeaving()
        {
            ColoredView view = new();
            MouseOverLocationCanvasViewEffect effect = new() { viewObj = view };

            view.Alpha = 0.9f;
            effect.viewObj = null;

            Assert.AreEqual(0.9f, view.Alpha, 1 / 255f, "leaving the view must not put the old opacity back");
        }
    }
}
