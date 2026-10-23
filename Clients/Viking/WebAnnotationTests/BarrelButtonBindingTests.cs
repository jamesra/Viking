using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the barrel button binding: which Windows pen report counts as the barrel button, that bad stored values fall back to
    /// the Windows barrel flag, and the sideways wheel message decoding that the paintbrush uses.
    /// </summary>
    [TestClass]
    public class BarrelButtonBindingTests
    {
        private BarrelButtonKind saved;

        [TestInitialize]
        public void Save() => saved = BarrelButtonBinding.Current;

        [TestCleanup]
        public void Restore() => BarrelButtonBinding.Current = saved;

        [TestMethod]
        public void Default_IsTheWindowsBarrelFlag()
        {
            BarrelButtonBinding.Current = BarrelButtonKind.Barrel;

            Assert.IsTrue(BarrelButtonBinding.IsDown(PenFlags.Barrel));
            Assert.IsFalse(BarrelButtonBinding.IsDown(PenFlags.Eraser));
            Assert.IsFalse(BarrelButtonBinding.IsDown(PenFlags.None));
        }

        [TestMethod]
        public void EraserBinding_TreatsTheEraserReportAsTheBarrelButton()
        {
            BarrelButtonBinding.Current = BarrelButtonKind.Eraser;

            Assert.IsTrue(BarrelButtonBinding.IsDown(PenFlags.Eraser));
            Assert.IsFalse(BarrelButtonBinding.IsDown(PenFlags.Barrel));
            Assert.IsFalse(BarrelButtonBinding.IsEraserDown(PenFlags.Eraser), "the eraser must not also pan the view");
        }

        [TestMethod]
        public void BarrelBinding_EraserStillMeansEraser()
        {
            BarrelButtonBinding.Current = BarrelButtonKind.Barrel;

            Assert.IsTrue(BarrelButtonBinding.IsEraserDown(PenFlags.Eraser));
            Assert.IsFalse(BarrelButtonBinding.IsEraserDown(PenFlags.Barrel));
        }

        [TestMethod]
        public void IsDown_WithOtherFlagsSet_StillFindsTheButton()
        {
            BarrelButtonBinding.Current = BarrelButtonKind.Barrel;

            Assert.IsTrue(BarrelButtonBinding.IsDown(PenFlags.Barrel | PenFlags.Inverted));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("Nonsense")]
        [DataRow("42")]
        public void Parse_UnknownText_IsBarrel(string text) =>
            Assert.AreEqual(BarrelButtonKind.Barrel, BarrelButtonBinding.Parse(text));

        [TestMethod]
        public void FormatThenParse_RoundTrips()
        {
            foreach (BarrelButtonKind kind in Enum.GetValues(typeof(BarrelButtonKind)))
                Assert.AreEqual(kind, BarrelButtonBinding.Parse(BarrelButtonBinding.Format(kind)));
        }

        [TestMethod]
        public void Parse_IsCaseInsensitive() =>
            Assert.AreEqual(BarrelButtonKind.Eraser, BarrelButtonBinding.Parse("eraser"));

        [TestMethod]
        public void HorizontalWheel_Delta_ReadsTheSignedHighWord()
        {
            Assert.AreEqual(120, HorizontalWheelMessage.Delta(new IntPtr(120L << 16)));
            Assert.AreEqual(-120, HorizontalWheelMessage.Delta(new IntPtr(0xFF88L << 16)), "0xFF88 is -120 as a 16-bit word");
            Assert.AreEqual(0, HorizontalWheelMessage.Delta(new IntPtr(0x0008)), "key-state bits in the low word are not the delta");
        }

        [TestMethod]
        public void HorizontalWheel_ScreenPosition_ReadsSignedWords()
        {
            var point = HorizontalWheelMessage.ScreenPosition(new IntPtr((0xFFFBL << 16) | 0xFFF6L));

            Assert.AreEqual(-10, point.X);
            Assert.AreEqual(-5, point.Y);
            Assert.AreEqual(300, HorizontalWheelMessage.ScreenPosition(new IntPtr((200L << 16) | 300)).X);
        }

        [TestMethod]
        public void HorizontalWheel_RecognizesBothMessages()
        {
            Assert.IsTrue(HorizontalWheelMessage.IsHorizontalWheel(0x020E));
            Assert.IsTrue(HorizontalWheelMessage.IsHorizontalWheel(0x024F));
            Assert.IsFalse(HorizontalWheelMessage.IsHorizontalWheel(0x020A), "the ordinary wheel is not it");
        }
    }
}
