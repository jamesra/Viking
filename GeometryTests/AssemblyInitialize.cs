using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class AssemblyInitialize
    {
        [AssemblyInitializeAttribute]
        public static void InitializeMathNetMKL(TestContext context)
        {
            try
            {
                MathNet.Numerics.Control.UseNativeMKL();
            }
            catch (System.NotSupportedException)
            {
                // MKL not available, use managed implementation
                System.Console.WriteLine("MKL not available, using managed Math.NET implementation");
            }
        }
    }
}
