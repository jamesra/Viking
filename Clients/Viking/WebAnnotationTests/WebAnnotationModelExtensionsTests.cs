using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the SQL-free helpers in <c>WebAnnotationModel/Extensions.cs</c>: the bounding rectangle
    /// conversions the region queries send to the server, and the null-safe event raiser.
    /// </summary>
    [TestClass]
    public class WebAnnotationModelExtensionsTests
    {
        [TestMethod]
        public void ToBoundingRectangle_MapsLeftRightBottomTopToXMinXMaxYMinYMax()
        {
            AnnotationService.Types.BoundingRectangle bbox = new Rectangle(1, 3, 5, 9).ToBoundingRectangle();

            Assert.AreEqual(1, bbox.XMin);
            Assert.AreEqual(3, bbox.XMax);
            Assert.AreEqual(5, bbox.YMin);
            Assert.AreEqual(9, bbox.YMax);
        }

        [TestMethod]
        public void BoundingRectangle_RoundTripsThroughRectangle()
        {
            Rectangle original = new(-4.5, 12.25, -7, 30);

            Rectangle back = original.ToBoundingRectangle().ToRectangle();

            Assert.AreEqual(original.Left, back.Left);
            Assert.AreEqual(original.Right, back.Right);
            Assert.AreEqual(original.Bottom, back.Bottom);
            Assert.AreEqual(original.Top, back.Top);
        }

        [TestMethod]
        public void RaiseEventOnUIThread_NullEvent_DoesNothing()
        {
            PropertyChangedEventHandler handler = null;
            handler.RaiseEventOnUIThread(this, new PropertyChangedEventArgs("x"));
        }

        [TestMethod]
        public void RaiseEventOnUIThread_PlainTarget_InvokesSynchronouslyWithArgs()
        {
            string seen = null;
            object seenSender = null;
            PropertyChangedEventHandler handler = (s, e) => { seenSender = s; seen = e.PropertyName; };

            handler.RaiseEventOnUIThread(this, new PropertyChangedEventArgs("Name"));

            Assert.AreEqual("Name", seen);
            Assert.AreSame(this, seenSender);
        }
    }
}
