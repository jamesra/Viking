using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using VikingXNAGraphics;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the logic that decides whether a thrown exception is a DXGI device-removed failure
    /// (it gates the GPU-removed dialog) and how the overlay message reports the HRESULT.
    /// </summary>
    [TestClass]
    public class GpuDeviceRemovedDiagnosticsTests
    {
        const int E_FAIL = unchecked((int)0x80004005);

        /// <summary>Exception whose <see cref="Exception.HResult"/> can be set (the setter is protected).</summary>
        sealed class HResultException : Exception
        {
            public HResultException(int hresult, Exception? inner = null) : base("test", inner) => HResult = hresult;
        }

        [TestMethod]
        [DataRow(GpuDeviceRemovedDiagnostics.DXGI_ERROR_DEVICE_REMOVED, true)]
        [DataRow(GpuDeviceRemovedDiagnostics.DXGI_ERROR_DEVICE_HUNG, true)]
        [DataRow(GpuDeviceRemovedDiagnostics.DXGI_ERROR_DEVICE_RESET, true)]
        [DataRow(GpuDeviceRemovedDiagnostics.DXGI_ERROR_DRIVER_INTERNAL_ERROR, true)]
        [DataRow(GpuDeviceRemovedDiagnostics.DXGI_ERROR_INVALID_CALL, false)]
        [DataRow(GpuDeviceRemovedDiagnostics.DXGI_ERROR_WAS_STILL_DRAWING, false)]
        [DataRow(0, false)]
        [DataRow(E_FAIL, false)]
        public void OnlyTheFourRemovedCodesCountAsDeviceRemoved(int hr, bool expected)
        {
            Assert.AreEqual(expected, GpuDeviceRemovedDiagnostics.IsDeviceRemovedHResult(hr));
        }

        [TestMethod]
        public void NullExceptionIsNotDeviceRemoved()
        {
            Assert.IsFalse(GpuDeviceRemovedDiagnostics.IsDeviceRemovedError(null));
        }

        [TestMethod]
        public void TopLevelRemovedHResultIsDetected()
        {
            Assert.IsTrue(GpuDeviceRemovedDiagnostics.IsDeviceRemovedError(
                new HResultException(GpuDeviceRemovedDiagnostics.DXGI_ERROR_DEVICE_REMOVED)));
        }

        [TestMethod]
        public void RemovedHResultNestedTwoInnerExceptionsDeepIsDetected()
        {
            var removed = new HResultException(GpuDeviceRemovedDiagnostics.DXGI_ERROR_DEVICE_HUNG);
            var middle = new HResultException(E_FAIL, removed);
            var outer = new HResultException(E_FAIL, middle);

            Assert.IsTrue(GpuDeviceRemovedDiagnostics.IsDeviceRemovedError(outer));
        }

        [TestMethod]
        public void ChainWithoutRemovedHResultIsNotDeviceRemoved()
        {
            var inner = new HResultException(GpuDeviceRemovedDiagnostics.DXGI_ERROR_INVALID_CALL);
            var outer = new HResultException(E_FAIL, inner);

            Assert.IsFalse(GpuDeviceRemovedDiagnostics.IsDeviceRemovedError(outer));
        }

        [TestMethod]
        public void UnknownHResultFallsBackToGenericNameAndLink()
        {
            Assert.AreEqual("unknown DXGI/HRESULT", GpuDeviceRemovedDiagnostics.NameForHResult(E_FAIL));
            StringAssert.Contains(GpuDeviceRemovedDiagnostics.DescribeHResult(E_FAIL), "learn.microsoft.com");
        }

        [TestMethod]
        public void OverlayMessageWithNullDeviceAndNullExceptionReportsNoQueryAndSOk()
        {
            string message = GpuDeviceRemovedDiagnostics.FormatOverlayMessage(null, null);

            StringAssert.Contains(message, "GetDeviceRemovedReason: (could not query)");
            StringAssert.Contains(message, "Exception HRESULT: S_OK (0x00000000)");
        }

        [TestMethod]
        public void OverlayMessageReportsNestedRemovedHResultInHex()
        {
            var ex = new HResultException(E_FAIL,
                new HResultException(GpuDeviceRemovedDiagnostics.DXGI_ERROR_DEVICE_HUNG));

            string message = GpuDeviceRemovedDiagnostics.FormatOverlayMessage(null, ex);

            StringAssert.Contains(message, "Exception HRESULT: DXGI_ERROR_DEVICE_HUNG (0x887A0006)");
        }

        [TestMethod]
        public void OverlayMessageWithoutRemovedHResultReportsTheOuterHResult()
        {
            var ex = new HResultException(E_FAIL,
                new HResultException(GpuDeviceRemovedDiagnostics.DXGI_ERROR_INVALID_CALL));

            string message = GpuDeviceRemovedDiagnostics.FormatOverlayMessage(null, ex);

            StringAssert.Contains(message, "Exception HRESULT: unknown DXGI/HRESULT (0x80004005)");
        }
    }
}
