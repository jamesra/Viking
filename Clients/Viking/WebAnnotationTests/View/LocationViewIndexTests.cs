using AnnotationService.Types;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotationTests.View
{
    /// <summary>
    /// Pins <see cref="LocationViewIndex{TView}"/>, the bookkeeping behind the location views a section draws. A user moved
    /// a circle, it saved, and it stayed drawn at its old place for good: the next drag started from the saved place and
    /// every later move "snapped back". These tests cover the ways the view could stop following the location: a view
    /// rebuilt from the old geometry mid-change, a store that swaps in a new object for the ID, and an add that throws.
    /// The harness handles property changes the way <see cref="SectionAnnotationsView"/> does, with a dictionary as the store.
    /// </summary>
    [TestClass]
    public class LocationViewIndexTests
    {
        private const long Id = 42;
        private const int Section = 7;

        /// <summary>A view that records the object and centre it was built from.</summary>
        private sealed class FakeView(LocationObj from)
        {
            public readonly LocationObj From = from;
            public readonly Vector2 Center = from.Position;
        }

        private sealed class Harness
        {
            public readonly Dictionary<long, LocationObj> Store = [];
            public readonly List<LocationObj> Heard = [];
            public readonly LocationViewIndex<FakeView> Index;

            /// <summary>When set, the next bounds lookup throws (once).</summary>
            public bool ThrowOnNextBounds;

            /// <summary>Runs after the view is dropped on a geometry Changing, before the shape changes (a region load on another thread).</summary>
            public Action<LocationObj>? BetweenChangingAndChanged;

            public Harness(Func<LocationObj, FakeView?>? createView = null)
            {
                Index = new LocationViewIndex<FakeView>(
                    id => Store.TryGetValue(id, out LocationObj current) ? current : null,
                    createView ?? (loc => new FakeView(loc)),
                    Bounds,
                    Subscribe,
                    Unsubscribe);
            }

            private RTree.Rectangle Bounds(FakeView view)
            {
                if (ThrowOnNextBounds)
                {
                    ThrowOnNextBounds = false;
                    throw new InvalidOperationException("index add failed");
                }

                return Box(view.Center);
            }

            private void Subscribe(LocationObj loc)
            {
                Heard.Add(loc);
                loc.PropertyChanging += OnChanging;
                loc.PropertyChanged += OnChanged;
            }

            private void Unsubscribe(LocationObj loc)
            {
                Heard.Remove(loc);
                loc.PropertyChanging -= OnChanging;
                loc.PropertyChanged -= OnChanged;
            }

            private void OnChanging(object sender, PropertyChangingEventArgs e)
            {
                if (!LocationObj.IsGeometryProperty(e.PropertyName))
                    return;

                LocationObj loc = (LocationObj)sender;
                Index.RemoveView(loc.ID);
                BetweenChangingAndChanged?.Invoke(loc);
            }

            private void OnChanged(object sender, PropertyChangedEventArgs e)
            {
                if (!LocationObj.IsGeometryProperty(e.PropertyName))
                    return;

                LocationObj loc = (LocationObj)sender;
                Index.RemoveView(loc.ID);
                Index.Add(loc);
            }

            public Vector2 DrawnCenter => Index.Views[Id].Center;

            /// <summary>The IDs the spatial index finds at <paramref name="p"/> (hit testing and drawing go through it).</summary>
            public List<long> FoundAt(Vector2 p) => Index.Search.Intersects(Box(p));
        }

        private static RTree.Rectangle Box(Vector2 c) => new(c.X - 1, c.Y - 1, c.X + 1, c.Y + 1, Section, Section);

        private static LocationObj Circle(double x, double y)
        {
            Microsoft.SqlServer.Types.SqlGeometry shape = SqlGeometryUtils.Extensions.ToCircle(x, y, Section, 5);
            return new LocationObj(new Location
            {
                ID = Id,
                ParentID = 1,
                TypeCode = (short)LocationType.CIRCLE,
                Section = Section,
                Position = new AnnotationPoint(x, y, Section),
                MosaicShapeWKB = shape.AsBinary(),
                VolumeShapeWKB = shape.AsBinary()
            });
        }

        private static void MoveTo(LocationObj loc, double x, double y) =>
            loc.MosaicShape = SqlGeometryUtils.Extensions.ToCircle(x, y, Section, 5);

        [TestMethod]
        public void MovingTheLocationRebuildsTheViewAtTheNewCentre()
        {
            Harness h = new();
            LocationObj loc = Circle(10, 10);
            h.Store[Id] = loc;
            h.Index.Add(loc);

            MoveTo(loc, 100, 200);

            Assert.AreEqual(new Vector2(100, 200), h.DrawnCenter);
            CollectionAssert.AreEqual(new[] { Id }, h.FoundAt(new Vector2(100, 200)));
            Assert.AreEqual(0, h.FoundAt(new Vector2(10, 10)).Count, "Nothing may be left at the old place.");
        }

        /// <summary>
        /// A region load re-adds the location between the Changing removal and the Changed re-add, from the old geometry.
        /// The re-add used to see the ID as known and do nothing, leaving the view at the old place.
        /// </summary>
        [TestMethod]
        public void AViewRebuiltFromTheOldGeometryMidChangeIsReplaced()
        {
            Harness h = new();
            LocationObj loc = Circle(10, 10);
            h.Store[Id] = loc;
            h.Index.Add(loc);
            h.BetweenChangingAndChanged = l => h.Index.Add(l);

            MoveTo(loc, 100, 200);

            Assert.AreEqual(new Vector2(100, 200), h.DrawnCenter);
            Assert.AreEqual(0, h.FoundAt(new Vector2(10, 10)).Count);
        }

        /// <summary>
        /// The section is evicted and loaded again while a region load still holds the old object: the store's add of the new
        /// object is handled before the old object's removal, and the stale load re-adds the old one after it. Keyed by ID,
        /// the view kept the old object and the new one was never heard, so moving it never moved the view.
        /// </summary>
        [TestMethod]
        public void AnObjectReplacedInTheStoreStillDrivesItsView()
        {
            Harness h = new();
            LocationObj old = Circle(10, 10);
            h.Store[Id] = old;
            h.Index.Add(old);

            LocationObj reloaded = Circle(10, 10);
            h.Store[Id] = reloaded;
            h.Index.Add(reloaded);
            h.Index.Remove(old);
            h.Index.Add(old);

            Assert.AreSame(reloaded, h.Index.Views[Id].From, "The view must be built from the store's object.");
            CollectionAssert.AreEqual(new[] { reloaded }, h.Heard, "Only the store's object may be heard.");
            Assert.IsTrue(h.Index.HasCurrentView(Id));

            MoveTo(reloaded, 100, 200);

            Assert.AreEqual(new Vector2(100, 200), h.DrawnCenter);
            Assert.AreEqual(0, h.FoundAt(new Vector2(10, 10)).Count);
        }

        /// <summary>The same swap with the removal handled first, and the stale load arriving before the new object is in the store.</summary>
        [TestMethod]
        public void AStaleObjectAddedAfterItLeftTheStoreGetsNoView()
        {
            Harness h = new();
            LocationObj old = Circle(10, 10);
            h.Store[Id] = old;
            h.Index.Add(old);

            h.Store.Remove(Id);
            h.Index.Remove(old);
            h.Index.Add(old);

            Assert.IsFalse(h.Index.Views.ContainsKey(Id));
            Assert.AreEqual(0, h.Heard.Count);

            LocationObj reloaded = Circle(10, 10);
            h.Store[Id] = reloaded;
            h.Index.Add(reloaded);
            MoveTo(reloaded, 100, 200);

            Assert.AreEqual(new Vector2(100, 200), h.DrawnCenter);
            CollectionAssert.AreEqual(new[] { reloaded }, h.Heard);
        }

        /// <summary>
        /// An add that throws after the view is made (the spatial index refused it) used to leave the view registered without
        /// its ID, so every later add and remove skipped it and the view was frozen. Now nothing is left and the next add works.
        /// </summary>
        [TestMethod]
        public void AThrowingAddLeavesNoHalfRegisteredView()
        {
            Harness h = new();
            LocationObj loc = Circle(10, 10);
            h.Store[Id] = loc;
            h.ThrowOnNextBounds = true;

            Assert.ThrowsException<InvalidOperationException>(() => h.Index.Add(loc));

            Assert.IsFalse(h.Index.Views.ContainsKey(Id));
            Assert.IsFalse(h.Index.Search.Contains(Id));
            Assert.IsFalse(h.Index.HasCurrentView(Id));

            MoveTo(loc, 100, 200);

            Assert.AreEqual(new Vector2(100, 200), h.DrawnCenter);
            CollectionAssert.AreEqual(new[] { Id }, h.FoundAt(new Vector2(100, 200)));
        }

        /// <summary>A location whose view cannot be built is still heard, so it is unheard when it leaves the store, and removing it does not throw.</summary>
        [TestMethod]
        public void RemoveUnsubscribesALocationWithoutAView()
        {
            Harness h = new(createView: _ => null);
            LocationObj loc = Circle(10, 10);
            h.Store[Id] = loc;

            Assert.IsFalse(h.Index.Add(loc));
            CollectionAssert.AreEqual(new[] { loc }, h.Heard);

            h.Store.Remove(Id);
            h.Index.Remove(loc);

            Assert.AreEqual(0, h.Heard.Count);
            Assert.IsFalse(h.Index.Views.Any());
        }
    }
}
