using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class SegmentationRpcErrorsTests
    {
        private static RpcException Failure(StatusCode code, string detail = "")
            => new(new Status(code, detail));

        [TestMethod]
        public void DeadlineExceededSaysTheServerTimedOutAndKeepsItsDetail()
        {
            string? text = SegmentationRpcErrors.Describe(
                Failure(StatusCode.DeadlineExceeded, "No TilesAnswer within 60s of TilesNeeded."));

            Assert.IsNotNull(text);
            StringAssert.Contains(text, "timed out");
            StringAssert.Contains(text, "try again");
            StringAssert.Contains(text, "No TilesAnswer within 60s");
        }

        [TestMethod]
        public void ResourceExhaustedSaysTheServerIsBusyOrOverALimit()
        {
            string? text = SegmentationRpcErrors.Describe(
                Failure(StatusCode.ResourceExhausted, "Request has 70 tiles; the server accepts at most 64 (SEGMENTATION_MAX_TILES)."));

            Assert.IsNotNull(text);
            StringAssert.Contains(text, "busy");
            StringAssert.Contains(text, "SEGMENTATION_MAX_TILES");
        }

        [TestMethod]
        public void AMissingDetailLeavesNoEmptyParentheses()
        {
            string? text = SegmentationRpcErrors.Describe(Failure(StatusCode.ResourceExhausted));

            Assert.IsNotNull(text);
            Assert.IsFalse(text.Contains("()"));
        }

        [DataTestMethod]
        [DataRow(StatusCode.NotFound)]
        [DataRow(StatusCode.Unavailable)]
        [DataRow(StatusCode.FailedPrecondition)]
        [DataRow(StatusCode.Internal)]
        public void OtherStatusesKeepTheGenericHandling(StatusCode code)
        {
            Assert.IsNull(SegmentationRpcErrors.Describe(Failure(code, "x")));
        }
    }
}
