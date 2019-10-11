using System;
using Geometry;
using System.Collections.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class PathTest
    {
        bool? LastLoopEventValue = new bool?(); //True if there was a loop, otherwise false

        private void OnLoopChanged(object sender, bool HasLoop)
        {
            LastLoopEventValue = HasLoop;
        }

        private void ResetLoopEvent()
        {
            LastLoopEventValue = new bool?();
        }

        private void CheckLoopEventAndReset(bool? expected = new bool?())
        {
            Assert.AreEqual(LastLoopEventValue, expected);
            ResetLoopEvent();
        }

        /// <summary>
        /// Does not have a value if the event has not fired
        /// </summary>
        NotifyCollectionChangedAction? LastCollectionEventAction = new NotifyCollectionChangedAction?();

        private void OnPathChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            LastCollectionEventAction = e.Action;
        }

        private void ResetPathChangeEvent()
        {
            LastCollectionEventAction = new NotifyCollectionChangedAction?();
        }

        private void CheckCollectionEventAndReset(NotifyCollectionChangedAction? expected = new NotifyCollectionChangedAction?())
        {
            Assert.AreEqual(LastCollectionEventAction, expected);
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

        private void CompareWithExpectedLoop(GridVector2[] loop, GridVector2[] expected_loop)
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
            GridVector2[] expected_loop = new GridVector2[]
            {
                new GridVector2(0,0),
                new GridVector2(10,0),
                new GridVector2(0,10),
                new GridVector2(0,0)
            };

            Path path = new Path();
            SubscribeToEvents(path);

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(-10, 0));
            CheckCollectionEventAndReset(NotifyCollectionChangedAction.Add);
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(10, 0));
            CheckCollectionEventAndReset(NotifyCollectionChangedAction.Add);
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            //Ensure there was no loop event fired yet
            CheckLoopEventAndReset();
            path.Push(new GridVector2(0, -10));
             
            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CheckLoopEventAndReset(true);  //Event should fire for loop addition
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new GridVector2(-10, -10));
            CheckLoopEventAndReset(); //No event expected because the loop did not change
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            CheckCollectionEventAndReset(NotifyCollectionChangedAction.Remove);
            CheckLoopEventAndReset(); //No event expected because the loop did not change
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);
            CheckLoopEventAndReset(false); //Event should fire for loop removal

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new GridVector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CheckLoopEventAndReset(true); //Event should fire for loop addition
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Move the endpoint to be exactly on the segment, ensure the loop event and change events fire.
            //The loop is different and the path has changed
            path.Replace(new GridVector2(1, 0));
            CheckCollectionEventAndReset(NotifyCollectionChangedAction.Replace);
            CheckLoopEventAndReset(true);


            path.Clear();
            Assert.IsTrue(path.Points.Count == 0);
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
            GridVector2[] expected_loop = new GridVector2[]
            {
                new GridVector2(0,0),
                new GridVector2(10,0),
                new GridVector2(0,10),
                new GridVector2(0,0)
            };

            Path path = new Path();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(-10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 0));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new GridVector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new GridVector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

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
            GridVector2[] expected_loop = new GridVector2[]
            {
                new GridVector2(0,0),
                new GridVector2(10,0),
                new GridVector2(0,10),
                new GridVector2(0,0)
            };

            Path path = new Path();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(-10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 10));

            //Make sure the loop was found
            path.Push(new GridVector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new GridVector2(-10, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new GridVector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);
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
            GridVector2[] expected_loop = new GridVector2[]
            {
                new GridVector2(0,0),
                new GridVector2(10,0),
                new GridVector2(0,10),
                new GridVector2(0,0)
            };

            Path path = new Path();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(-10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 10));

            //Make sure the loop was found
            path.Push(new GridVector2(0, 0));
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new GridVector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new GridVector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);
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
            GridVector2[] expected_loop = new GridVector2[]
            {
                new GridVector2(0,0),
                new GridVector2(10,0),
                new GridVector2(10,10),
                new GridVector2(0,10),
                new GridVector2(0,0)
            };

            Path path = new Path();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(-10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(10, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 10));
            Assert.IsFalse(path.HasSelfIntersection);

            //Make sure the loop was found
            path.Push(new GridVector2(0, 0));
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new GridVector2(0,-10));
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new GridVector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);
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
            GridVector2[] expected_loop = new GridVector2[]
            {
                new GridVector2(0,0),
                new GridVector2(10,0),
                new GridVector2(10,10),
                new GridVector2(0,10),
                new GridVector2(0,0)
            };

            Path path = new Path();

            //Build our path until we have a loop
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(10, 0));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(10, 10));
            Assert.IsFalse(path.HasSelfIntersection);
            path.Push(new GridVector2(0, 10));
            Assert.IsFalse(path.HasSelfIntersection);

            //Make sure the loop was found
            path.Push(new GridVector2(0, 0));
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Make sure the loop doesn't change with an extra random point
            path.Push(new GridVector2(0, -10));
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove a point and ensure the loop doesn't change
            path.Pop();
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);

            //Remove the point needed for an intersection and ensure the loop goes away
            path.Pop();
            Assert.IsFalse(path.HasSelfIntersection);

            //Replace the point needed for an intersection and ensure the loop comes back
            path.Push(new GridVector2(0, -10));

            //Make sure the loop was found
            Assert.IsTrue(path.HasSelfIntersection);
            CompareWithExpectedLoop(path.Loop, expected_loop);
        }
    }
}
