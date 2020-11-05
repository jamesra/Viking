using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class AssemblyInitialize
    {
        [AssemblyInitializeAttribute]
        public static void InitializeMathNetMKL(TestContext context)
        {
            MathNet.Numerics.Control.UseNativeMKL();
        }
    }
}
