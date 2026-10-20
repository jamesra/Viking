using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;

namespace WebAnnotationTests
{
    /// <summary>
    /// Jotunn uses <see cref="WebAnnotation.Global"/> without ever opening a volume in Viking's UI state. Its type
    /// initializer must not depend on that state, or the first use throws and every later use fails too.
    /// </summary>
    [TestClass]
    public class GlobalWithoutVikingVolumeTests
    {
        [TestMethod]
        public void LastEditedAnnotationIsUsableWithoutAVikingVolume()
        {
            var savedVolume = State.volume;
            long? savedId = WebAnnotation.Global.LastEditedAnnotationID;
            State.volume = null;
            try
            {
                WebAnnotation.Global.LastEditedAnnotationID = 42;
                Assert.AreEqual(42L, WebAnnotation.Global.LastEditedAnnotationID);
            }
            finally
            {
                WebAnnotation.Global.LastEditedAnnotationID = savedId;
                State.volume = savedVolume;
            }
        }
    }
}
