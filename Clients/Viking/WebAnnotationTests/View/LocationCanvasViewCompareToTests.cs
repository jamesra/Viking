using AnnotationService.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using WebAnnotation.View;
using WebAnnotationModel;

namespace WebAnnotationTests.View
{
    /// <summary>
    /// Pins <see cref="LocationCanvasView"/> ordering by 64-bit location ID. The old <c>(int)(ID - other.ID)</c>
    /// wrapped once IDs differed by 2^31 or more, so <c>SectionAnnotationsView.AdjacentLocationsNotOverlappedInRegion</c>'s
    /// <see cref="SortedSet{T}"/> dropped or misordered distinct locations.
    /// </summary>
    [TestClass]
    public class LocationCanvasViewCompareToTests
    {
        private const long TwoTo31 = 1L << 31;
        private const long TwoTo32 = 1L << 32;

        [TestMethod]
        [DataRow(1L, 2L, DisplayName = "small IDs")]
        [DataRow(0L, TwoTo31, DisplayName = "differ by exactly 2^31 (old cast gave int.MinValue)")]
        [DataRow(1L, TwoTo31 + 2, DisplayName = "differ by more than 2^31 (old cast flipped sign)")]
        [DataRow(5L, TwoTo32 + 5, DisplayName = "differ by exactly 2^32 (old cast truncated to 0)")]
        [DataRow(-TwoTo32, TwoTo32, DisplayName = "negative and positive IDs 2^33 apart")]
        [DataRow(long.MinValue, long.MaxValue, DisplayName = "full long range (old subtraction overflowed)")]
        public void ComparesByIdWithoutWrapping(long lower, long higher)
        {
            IComparable<LocationCanvasView> low = View(lower);
            IComparable<LocationCanvasView> high = View(higher);

            Assert.IsTrue(low.CompareTo((LocationCanvasView)high) < 0, "lower ID sorts first");
            Assert.IsTrue(high.CompareTo((LocationCanvasView)low) > 0, "higher ID sorts last");
            Assert.AreEqual(0, low.CompareTo(View(lower)), "same ID compares equal");
        }

        [TestMethod]
        public void NullSortsFirst()
        {
            IComparable<LocationCanvasView> view = View(1);
            Assert.IsTrue(view.CompareTo(null!) > 0);
        }

        [TestMethod]
        public void SortedSetKeepsEveryDistinctLocationInIdOrder()
        {
            long[] ids = [7, TwoTo32 + 7, TwoTo31 + 3, 3, TwoTo32 * 3 + 7, 1];
            SortedSet<LocationCanvasView> set = [];

            foreach (long id in ids)
            {
                Assert.IsTrue(set.Add(View(id)), $"location {id} is distinct and must be added");
            }

            Assert.IsFalse(set.Add(View(TwoTo32 + 7)), "a second view of the same location is not added");
            CollectionAssert.AreEqual(ids.OrderBy(id => id).ToArray(), set.Select(v => v.ID).ToArray());
        }

        /// <summary>
        /// A view that skips its constructor (which needs mapped geometry) and only knows its location ID.
        /// </summary>
        private static OverlappedLocationView View(long id)
        {
            OverlappedLocationView view = (OverlappedLocationView)FormatterServices.GetUninitializedObject(typeof(OverlappedLocationView));
            FieldInfo? model = typeof(LocationCanvasView).GetField("modelObj", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(model);
            model.SetValue(view, new LocationObj(new Location { ID = id }));
            return view;
        }
    }
}
