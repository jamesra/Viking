using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Viking.gRPC.SegmentationServiceTypes.V1;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// A response holds one segment per island, numbered from 1 by decreasing trusted area. Callers that keep one shape keep
    /// island 1 (<see cref="SegmentationExtensions.GetPrimaryIsland"/>); callers that keep every island walk them in island order.
    /// </summary>
    [TestClass]
    public class SegmentationResponseExtensionsTests
    {
        [TestMethod]
        public void APrimaryIslandOfNothingIsNull()
        {
            Assert.IsNull(new SegmentationResponse().GetPrimaryIsland());
            Assert.IsNull(((SegmentationResponse?)null).GetPrimaryIsland());
            Assert.AreEqual(0, ((SegmentationResponse?)null).GetSegmentsByIsland().Count());
        }

        [TestMethod]
        public void TheOnlyIslandIsThePrimaryOne()
        {
            SegmentResult only = new() { Island = 1, Score = 0.1f };
            SegmentationResponse response = new();
            response.Segments.Add(only);

            Assert.AreSame(only, response.GetPrimaryIsland());
        }

        [TestMethod]
        public void IslandOneIsPrimaryWhateverTheScoresAndTheOrderTheyArrivedIn()
        {
            SegmentResult second = new() { Island = 2, Score = 0.99f };
            SegmentResult first = new() { Island = 1, Score = 0.2f };
            SegmentResult third = new() { Island = 3, Score = 0.5f };
            SegmentationResponse response = new();
            response.Segments.Add(second);
            response.Segments.Add(first);
            response.Segments.Add(third);

            Assert.AreSame(first, response.GetPrimaryIsland(), "the largest island, not the most confident one");
            List<SegmentResult> ordered = [.. response.GetSegmentsByIsland()];
            CollectionAssert.AreEqual(new[] { first, second, third }, ordered);
        }
    }
}
