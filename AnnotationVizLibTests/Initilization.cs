using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationVizLibTests
{
    [TestClass]
    static class Initilization
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        { 
            //SqlServerTypes.TestUtilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
