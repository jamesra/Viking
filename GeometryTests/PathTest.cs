using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class PathTest
    {
        bool? LastLoopEventValue = new bool?(); //True if there was a loop, otherwise false

        private void OnLoopChanged(object sender, bool HasLoop) => LastLoopEventValue = HasLoop;

        private void ResetLoopEvent() => LastLoopEventValue = new bool?();

        private void CheckLoopEventAndReset(bool? expected = new bool?())
        {
            Assert.AreEqual(expected, LastLoopEventValue);
            ResetLoopEvent();
        }

        /// <summary>
        /// Does not have a value if the event has not fired
        /// </summary>
        NotifyCollectionChangedAction? LastCollectionEventAction = new NotifyCollectionChangedAction?();

        private void OnPathChanged(object sender, NotifyCollectionChangedEventArgs e) => LastCollectionEventAction = e.Action;

        private void ResetPathChangeEvent() => LastCollectionEventAction = new NotifyCollectionChangedAction?();

        private void CheckCollectionEventAndReset(NotifyCollectionChangedAction? expected = new NotifyCollectionChangedAction?())
        {
            Assert.AreEqual(expected, LastCollectionEventAction);
            ResetPathChangeEvent();
        }

        private void SubscribeToEvents(Path path)
        {
            path.OnLoopChanged += this.OnLoopChanged;
            path.OnPathChanged += this.OnPathChanged;
        }

        private void UnsubscribeToEvents(Path path)
        {
            path.OnLoopChanged -= this.OnLoopChanged;
            path.OnPathChanged -= this.OnPathChanged;
        }

        private static void CompareWithExpectedLoop(Vector2[] loop, Vector2[] expected_loop)
        {
            Assert.AreEqual(expected_loop.Length, loop.Length);
            for (int i = 0; i < expected_loop.Length; i++)
            {
                Assert.AreEqual(expected_loop[i], loop[i]);
            }
        }

        /// <summary>
        ///           C
        ///           |  \  
        ///           |    \
        ///           |      \ 
        ///           |        \
        ///  A ------ I -------- B 
        ///           |
        ///           |
        ///           |
        ///           |
        ///  E--------D
        /// 
        /// We expect to find the loop I,B,C,I
        /// 
        /// </summary>
        [TestMethod]
        public void TestLoopDetection()
        {
            Vector2[] expected_loop =
            [
                new(0,0),
                new(10,0),
                new(0,10),
                new(0,0)
            ];

            Path path = new();
            SubscribeToEvents(path);

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(-10, 0));
            CheckCollectionEventAndReset(NotifyCollectionChangedAction.Add);
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 0));
            CheckCollectionEventAndReset(NotifyCollectionChangedAction.Add);
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            //Ensure there was no loop event fired yet
            CheckLoopEventAndReset();
            path.Push(new Vector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CheckLoopEventAndReset(true);  //Event should fire for loop addition
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new Vector2(-10, -10));
            CheckLoopEventAndReset(); //No event expected because the loop did not change
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            CheckCollectionEventAndReset(NotifyCollectionChangedAction.Remove);
            CheckLoopEventAndReset(); //No event expected because the loop did not change
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);
            CheckLoopEventAndReset(false); //Event should fire for loop removal

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new Vector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CheckLoopEventAndReset(true); //Event should fire for loop addition
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Move the endpoint to be exactly on the segment, ensure the loop event and change events fire.
            //The loop is different and the path has changed
            path.Replace(new Vector2(1, 0));
            CheckCollectionEventAndReset(NotifyCollectionChangedAction.Replace);
            CheckLoopEventAndReset(true);


            path.Clear();
            Assert.AreEqual(0, path.Points.Count);
            CheckCollectionEventAndReset(NotifyCollectionChangedAction.Reset);
            UnsubscribeToEvents(path);
        }

        /// <summary>
        ///           C
        ///           |  \  
        ///           |    \
        ///           |      \ 
        ///           |        \
        ///  A ------ D -------- B 
        ///           |
        ///           |
        ///           |
        ///           |
        ///           E
        /// 
        /// We expect to find the loop D,B,C,D
        /// 
        /// </summary>
        [TestMethod]
        public void TestLoopOnEndpointDetection()
        {
            Vector2[] expected_loop =
            [
                new(0,0),
                new(10,0),
                new(0,10),
                new(0,0)
            ];

            Path path = new();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(-10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 0));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new Vector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new Vector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

        }


        /// <summary>
        ///           D
        ///           |  \  
        ///           |    \
        ///           |      \ 
        ///           |        \
        ///  A ------ B -------- C 
        ///           |
        ///           |
        ///           |
        ///           |
        ///           E
        /// 
        /// We expect to find the loop B,C,D,B
        /// 
        /// </summary>
        [TestMethod]
        public void TestLoopOnEndpointDetection2()
        {
            Vector2[] expected_loop =
            [
                new(0,0),
                new(10,0),
                new(0,10),
                new(0,0)
            ];

            Path path = new();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(-10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 10));

            //Make sure the loop was found
            path.Push(new Vector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new Vector2(-10, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new Vector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);
        }

        /// <summary>
        ///           D
        ///           |  \  
        ///           |    \
        ///           |      \ 
        ///           |        \
        ///  A ------ B/E -------- C 
        ///           |
        ///           |
        ///           |
        ///           |
        ///           F
        /// 
        /// We expect to find the loop B,C,D,B
        /// 
        /// </summary>
        [TestMethod]
        public void TestLoopOnEndpointDetection3()
        {
            Vector2[] expected_loop =
            [
                new(0,0),
                new(10,0),
                new(0,10),
                new(0,0)
            ];

            Path path = new();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(-10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 10));

            //Make sure the loop was found
            path.Push(new Vector2(0, 0));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new Vector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new Vector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);
        }

        /// <summary>
        ///      D----C
        ///      |    |    
        ///      |    |    
        ///      |    |      \ 
        ///      |    |        \
        ///  A --E----B
        ///      |
        ///      |
        ///      |
        ///      |
        ///      F
        /// 
        /// We expect to find the loop B,C,D,B
        /// 
        /// </summary>
        [TestMethod]
        public void TestLoopOnEndpointDetectionWithBox()
        {
            Vector2[] expected_loop =
            [
                new(0,0),
                new(10,0),
                new(10,10),
                new(0,10),
                new(0,0)
            ];

            Path path = new();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(-10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 10));
            Assert.IsFalse(path.HasSelfIntersection);

            //Make sure the loop was found
            path.Push(new Vector2(0, 0));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new Vector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new Vector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);
        }

        /// <summary>
        ///      D----C
        ///      |    |    
        ///      |    |    
        ///      |    |      \ 
        ///      |    |        \
        ///      A----B
        ///      |
        ///      |
        ///      |
        ///      |
        ///      F
        /// 
        /// We expect to find the loop A,B,C,A
        /// 
        /// </summary>
        [TestMethod]
        public void TestLoopOnEndpointDetectionWithBox2()
        {
            Vector2[] expected_loop =
            [
                new(0,0),
                new(10,0),
                new(10,10),
                new(0,10),
                new(0,0)
            ];

            Path path = new();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 10));
            Assert.IsFalse(path.HasSelfIntersection);

            //Make sure the loop was found
            path.Push(new Vector2(0, 0));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new Vector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new Vector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);
        }

        /// <summary>
        ///      E----D
        ///      |    |    
        ///      F    C   
        ///      |    |      \ 
        ///      |    |        \
        ///      A----B
        ///      |
        ///      |
        ///      |
        ///      |
        ///      F
        /// 
        /// We expect to find the loop A,B,C,A
        /// 
        /// </summary>
        [TestMethod]
        public void TestLoopOnEndpointDetectionWithBox3()
        {
            Vector2[] expected_loop =
            [
                new(0,0),
                new(10,0),
                new(10,5),
                new(10,10),
                new(0,10),
                new(0,5),
                new(0,0)
            ];

            Path path = new();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 5));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(10, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new Vector2(0, 5));
            Assert.IsFalse(path.HasSelfIntersection);

            //Make sure the loop was found
            path.Push(new Vector2(0, 0));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new Vector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new Vector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            PathTest.CompareWithExpectedLoop(path.Loop, expected_loop);
        }

        /// <summary>
        /// Paths of 3 to 40 points at volume-scale coordinates with strictly increasing X, so no two
        /// non-adjacent segments cross and loop detection never runs into a self-intersection.
        /// </summary>
        private static Gen<Vector2[]> MonotonePathPoints() =>
            from count in Gen.Choose(3, 40)
            from originX in Gen.Choose(0, 500000)
            from steps in Gen.ArrayOf(count, Gen.Choose(1, 2000))
            from ys in Gen.ArrayOf(count, Gen.Choose(-500000, 500000))
            select BuildMonotonePoints(originX, steps, ys);

        private static Vector2[] BuildMonotonePoints(int originX, int[] steps, int[] ys)
        {
            Vector2[] points = new Vector2[steps.Length];
            double x = originX;
            for (int i = 0; i < points.Length; i++)
            {
                x += steps[i] + 0.25;
                points[i] = new Vector2(x, ys[i] + 0.5);
            }

            return points;
        }

        private static Path BuildPath(IEnumerable<Vector2> points)
        {
            Path path = new();
            foreach (Vector2 p in points)
                path.Push(p);
            return path;
        }

        [TestMethod]
        public void Pop_ReturnsLastPushedPointAndKeepsPrefix() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(MonotonePathPoints()), points =>
                {
                    Path path = BuildPath(points);
                    Vector2 popped = path.Pop();
                    return popped == points[points.Length - 1] &&
                           path.Points.SequenceEqual(points.Take(points.Length - 1)) &&
                           path.Segments.Count == points.Length - 2;
                }),
                nameof(Pop_ReturnsLastPushedPointAndKeepsPrefix));

        [TestMethod]
        public void Pop_RemoveEventCarriesPoppedPoint()
        {
            Vector2 first = new(-10, 0);
            Vector2 last = new(20, 5);
            Path path = BuildPath([first, new Vector2(10, 0), last]);

            NotifyCollectionChangedEventArgs args = null;
            path.OnPathChanged += (sender, e) => args = e;

            Assert.AreEqual(last, path.Pop());
            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, args.Action);
            Assert.AreEqual(1, args.OldItems.Count);
            Assert.AreEqual(last, args.OldItems[0]);
        }

        [TestMethod]
        public void Replace_EventCarriesOldLastPoint()
        {
            Vector2 first = new(-10, 0);
            Vector2 oldLast = new(20, 5);
            Vector2 newLast = new(25, -5);
            Path path = BuildPath([first, new Vector2(10, 0), oldLast]);

            NotifyCollectionChangedEventArgs args = null;
            path.OnPathChanged += (sender, e) => args = e;

            path.Replace(newLast);

            Assert.IsNotNull(args);
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, args.Action);
            Assert.AreEqual(oldLast, args.OldItems[0]);
            Assert.AreEqual(newLast, args.NewItems[0]);
            Assert.AreEqual(2, args.NewStartingIndex);
            CollectionAssert.AreEqual(new[] { first, new Vector2(10, 0), newLast }, path.Points);
        }
    }
}
