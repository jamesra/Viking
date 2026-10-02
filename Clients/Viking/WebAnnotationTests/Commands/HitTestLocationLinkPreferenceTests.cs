using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Viking.AnnotationServiceTypes;
using VikingXNAGraphics;
using WebAnnotation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="HitTestResultExtensions.NearestObjectOnCurrentSectionThenAdjacent"/> lets a closer
    /// location link beat a current-section circle only for callers that opt in, and never lets an
    /// adjacent-section circle do so.
    /// </summary>
    [TestClass]
    public class HitTestLocationLinkPreferenceTests
    {
        private const int Section = 5;

        [TestMethod]
        public void DefaultKeepsCircleOverCloserLink()
        {
            HitTestResult circle = Circle(Section, 0.8);
            HitTestResult link = Link(Section + 1, 0.3);

            HitTestResult best = new List<HitTestResult> { circle, link }
                .NearestObjectOnCurrentSectionThenAdjacent(Section);

            Assert.AreSame(circle, best);
        }

        [TestMethod]
        public void OptInLetsCloserLinkBeatCircle()
        {
            HitTestResult circle = Circle(Section, 0.8);
            HitTestResult link = Link(Section + 1, 0.3);

            HitTestResult best = new List<HitTestResult> { circle, link }
                .NearestObjectOnCurrentSectionThenAdjacent(Section, preferCloserLocationLinks: true);

            Assert.AreSame(link, best);
        }

        [TestMethod]
        public void OptInKeepsCircleWhenLinkIsFarther()
        {
            HitTestResult circle = Circle(Section, 0.5);
            HitTestResult link = Link(Section + 1, 0.9);

            HitTestResult best = new List<HitTestResult> { circle, link }
                .NearestObjectOnCurrentSectionThenAdjacent(Section, preferCloserLocationLinks: true);

            Assert.AreSame(circle, best);
        }

        [TestMethod]
        public void OptInIgnoresLinkOutsideItsRadius()
        {
            HitTestResult circle = Circle(Section, 0.9);
            HitTestResult link = Link(Section + 1, 1.2);

            HitTestResult best = new List<HitTestResult> { circle, link }
                .NearestObjectOnCurrentSectionThenAdjacent(Section, preferCloserLocationLinks: true);

            Assert.AreSame(circle, best);
        }

        [TestMethod]
        public void OptInNeverLetsAdjacentSectionCircleBeatCurrentCircle()
        {
            HitTestResult circle = Circle(Section, 0.8);
            HitTestResult adjacentCircle = Circle(Section + 1, 0.1);

            HitTestResult best = new List<HitTestResult> { circle, adjacentCircle }
                .NearestObjectOnCurrentSectionThenAdjacent(Section, preferCloserLocationLinks: true);

            Assert.AreSame(circle, best);
        }

        private static HitTestResult Circle(int z, double distance) => new(new StubLocation(), z, 0, distance);

        private static HitTestResult Link(int z, double distance) => new(new StubLocationLink(), z, 0, distance);

        private sealed class StubLocation : IHitTesting, IViewLocation
        {
            public Rectangle BoundingBox => default;

            public long ID => 1;

            public bool Contains(Vector2 Position) => true;
        }

        private sealed class StubLocationLink : IHitTesting, IViewLocationLink
        {
            public Rectangle BoundingBox => default;

            public LocationLinkKey Key => new(1, 2);

            public bool Contains(Vector2 Position) => true;
        }
    }
}
