using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the null-safe event raiser in <c>WebAnnotationModel/Extensions.cs</c>.
    /// </summary>
    [TestClass]
    public class WebAnnotationModelExtensionsTests
    {
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
