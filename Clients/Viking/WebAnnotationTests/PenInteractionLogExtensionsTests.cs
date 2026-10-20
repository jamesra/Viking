using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins how the pen path's interaction log is turned into structure-link pass counts
    /// (<see cref="PenInteractionLogExtensions.CountStructurePasses"/>). A "scribble" from the origin
    /// structure to another structure and back counts passes per structure: 3 proposes a directional
    /// link, 4 a bidirectional one. Locations are built from plain data so no store or server is touched.
    /// </summary>
    [TestClass]
    public class PenInteractionLogExtensionsTests
    {
        private const long OriginStruct = 100;
        private const long OtherStruct = 200;
        private const long ThirdStruct = 300;

        private static readonly LocationObj Origin = Loc(1, OriginStruct);
        private static readonly LocationObj OriginSibling = Loc(2, OriginStruct);
        private static readonly LocationObj OtherA = Loc(10, OtherStruct);
        private static readonly LocationObj OtherB = Loc(11, OtherStruct);
        private static readonly LocationObj Third = Loc(20, ThirdStruct);

        private static LocationObj Loc(long id, long parent) => TestModel.Location(id, parent);

        private static (AnnotationRegionInteraction, LocationObj?) Enter(LocationObj? l) => (AnnotationRegionInteraction.ENTER, l);
        private static (AnnotationRegionInteraction, LocationObj?) Exit(LocationObj? l) => (AnnotationRegionInteraction.EXIT, l);

        /// <summary>Enter/exit the origin, then go to <paramref name="targets"/> and back in turn.</summary>
        private static List<(AnnotationRegionInteraction, LocationObj?)> Scribble(params LocationObj[] targets)
        {
            List<(AnnotationRegionInteraction, LocationObj?)> log = [Enter(Origin), Exit(Origin)];
            foreach (LocationObj target in targets)
            {
                log.Add(Enter(target));
                log.Add(Exit(target));
            }
            return log;
        }

        private static Dictionary<long, int> PassesByStructure(IReadOnlyList<(AnnotationRegionInteraction, LocationObj?)> log) =>
            PenInteractionLogExtensions.CountStructurePasses(log, Origin.ID, OriginStruct)
                .Values.GroupBy(v => v.Target.ParentID!.Value)
                .ToDictionary(g => g.Key, g => g.Max(v => v.Passes));

        [TestMethod]
        public void ThreePasses_OriginToOtherAndBackAndOver_CountsThree()
        {
            var log = Scribble(OtherA, Origin, OtherA);
            var counts = PenInteractionLogExtensions.CountStructurePasses(log, Origin.ID, OriginStruct);

            Assert.AreEqual(1, counts.Count);
            Assert.AreEqual(3, counts.Values.Single().Passes);
            Assert.AreSame(OtherA, counts.Values.Single().Target);
        }

        [TestMethod]
        public void FourPasses_EndingOnOrigin_CountsFour()
        {
            var log = Scribble(OtherA, Origin, OtherA, Origin);
            Assert.AreEqual(4, PassesByStructure(log)[OtherStruct]);
        }

        [TestMethod]
        public void ReturnToOriginSibling_CountsAsReturnToOriginStructure()
        {
            var log = Scribble(OtherA, OriginSibling, OtherA);
            Assert.AreEqual(3, PassesByStructure(log)[OtherStruct]);
        }

        [TestMethod]
        public void EventsBeforeTheOriginAndNullLocations_AreIgnored()
        {
            List<(AnnotationRegionInteraction, LocationObj?)> log = [Enter(OtherA), Exit(OtherA), Enter(null), Exit(null)];
            log.AddRange(Scribble(OtherA, Origin, OtherA));
            log.Insert(log.Count - 2, Enter(null));

            Assert.AreEqual(3, PassesByStructure(log)[OtherStruct]);
        }

        [TestMethod]
        public void ExitEvents_DoNotCount()
        {
            List<(AnnotationRegionInteraction, LocationObj?)> log = [Enter(Origin), Exit(OtherA), Exit(Origin), Exit(OtherA)];
            Assert.AreEqual(0, PenInteractionLogExtensions.CountStructurePasses(log, Origin.ID, OriginStruct).Count);
        }

        [TestMethod]
        public void OriginNeverEntered_CountsNothing()
        {
            List<(AnnotationRegionInteraction, LocationObj?)> log = [Enter(OtherA), Enter(OriginSibling), Enter(OtherA), Enter(OriginSibling)];
            Assert.AreEqual(0, PenInteractionLogExtensions.CountStructurePasses(log, Origin.ID, OriginStruct).Count);
        }

        [TestMethod]
        public void TwoStructures_AreCountedSeparately()
        {
            var log = Scribble(OtherA, Origin, Third, Origin);
            var passes = PassesByStructure(log);
            Assert.AreEqual(3, passes[OtherStruct]);
            Assert.AreEqual(2, passes[ThirdStruct]);
        }

        /// <summary>
        /// Regression: a structure with two locations on the section (a branch, two profiles) used to have
        /// its passes split by location ID, so a three-pass scribble that crossed both locations never
        /// reached the threshold. Passes belong to the structure, and one entry per structure is returned.
        /// </summary>
        [TestMethod]
        public void PassesAcrossTwoLocationsOfOneStructure_CountTogether()
        {
            var log = Scribble(OtherA, Origin, OtherB);
            var counts = PenInteractionLogExtensions.CountStructurePasses(log, Origin.ID, OriginStruct);

            Assert.AreEqual(1, counts.Count, "one entry per structure");
            Assert.AreEqual(3, counts.Values.Single().Passes);
            Assert.AreSame(OtherA, counts.Values.Single().Target, "the first location touched represents the structure");
        }
    }
}
