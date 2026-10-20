using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins HIWORD/LOWORD extraction from WM_POINTER wParam and lParam used by pen and touch input in NGVV.
    /// Win32 <c>MAKELPARAM</c> zero-extends, so on a 64-bit process a negative Y (pen above the primary monitor)
    /// arrives as an <see cref="IntPtr"/> above <see cref="int.MaxValue"/>.
    /// </summary>
    [TestClass]
    public class WinMsgInputWordTests
    {
        private static IntPtr MakeLParam(short x, short y) =>
            new IntPtr(unchecked((long)(((uint)(ushort)y << 16) | (ushort)x)));

        [TestMethod]
        public void Words_PositiveValues_SplitIntoLowAndHigh()
        {
            IntPtr p = MakeLParam(0x1234, 0x0056);

            Assert.AreEqual((ushort)0x1234, p.LowWord());
            Assert.AreEqual((ushort)0x0056, p.HiWord());
        }

        [TestMethod]
        public void SignedWords_NegativeYAsZeroExtendedLParam_DoNotThrow()
        {
            if (IntPtr.Size != 8) Assert.Inconclusive("Zero-extended DWORD above int.MaxValue only exists on 64-bit processes.");

            IntPtr p = MakeLParam(-5, -1200);

            Assert.AreEqual((short)-5, p.SignedLowWord());
            Assert.AreEqual((short)-1200, p.SignedHiWord());
        }

        [TestMethod]
        public void Words_RoundTripAnyXY()
        {
            if (IntPtr.Size != 8) Assert.Inconclusive("Zero-extended DWORD above int.MaxValue only exists on 64-bit processes.");

            Prop.ForAll<short, short>((x, y) =>
            {
                IntPtr p = MakeLParam(x, y);
                return p.SignedLowWord() == x && p.SignedHiWord() == y;
            }).QuickCheckThrowOnFailure();
        }
    }
}
