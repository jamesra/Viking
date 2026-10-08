using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Viking.gRPC.SegmentationServiceTypes.V1;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Pins <see cref="SegmentationExtensions.GetHighestScoringSegment"/> to the prior
    /// <c>OrderByDescending(s =&gt; s.Score).First()</c> rule used by debug overlay and auto-polygonize.
    /// </summary>
    [TestClass]
    public class SegmentationResponseExtensionsTests
    {
        [TestMethod]
        public void GetHighestScoringSegmentReturnsNullWhenEmpty()
        {
            Assert.IsNull(new SegmentationResponse().GetHighestScoringSegment());
            Assert.IsNull(((SegmentationResponse?)null).GetHighestScoringSegment());
        }

        [TestMethod]
        public void GetHighestScoringSegmentReturnsTheOnlySegment()
        {
            var response = new SegmentationResponse();
            var only = new SegmentResult { Score = 0.25f, Index = 3 };
            response.Segments.Add(only);

            Assert.AreSame(only, response.GetHighestScoringSegment());
        }

        [TestMethod]
        public void GetHighestScoringSegmentPicksMaxScore()
        {
            var response = new SegmentationResponse();
            response.Segments.Add(new SegmentResult { Score = 0.1f, Index = 0 });
            var best = new SegmentResult { Score = 0.9f, Index = 1 };
            response.Segments.Add(best);
            response.Segments.Add(new SegmentResult { Score = 0.5f, Index = 2 });

            Assert.AreSame(best, response.GetHighestScoringSegment());
        }

        [TestMethod]
        public void GetHighestScoringSegmentMatchesOrderByDescendingFirstOnTies()
        {
            var response = new SegmentationResponse();
            var firstTie = new SegmentResult { Score = 0.5f, Index = 1 };
            var secondTie = new SegmentResult { Score = 0.5f, Index = 2 };
            response.Segments.Add(firstTie);
            response.Segments.Add(secondTie);

            SegmentResult? fromHelper = response.GetHighestScoringSegment();
            SegmentResult fromLinq = response.Segments.OrderByDescending(s => s.Score).First();

            Assert.AreSame(fromLinq, fromHelper);
            Assert.AreSame(firstTie, fromHelper);
        }

        [TestMethod]
        public void GetSegmentsByDescendingScoreMatchesOrderByDescendingOnScores()
        {
            var response = new SegmentationResponse();
            var low = new SegmentResult { Score = 0.1f, Index = 0 };
            var mid = new SegmentResult { Score = 0.5f, Index = 1 };
            var high = new SegmentResult { Score = 0.9f, Index = 2 };
            response.Segments.Add(mid);
            response.Segments.Add(high);
            response.Segments.Add(low);

            List<SegmentResult> fromHelper = [.. response.GetSegmentsByDescendingScore()];
            List<SegmentResult> fromLinq = response.Segments.OrderByDescending(s => s.Score).ToList();

            CollectionAssert.AreEqual(fromLinq, fromHelper);
        }
    }
}
