using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationVizLibTests
{
    [TestClass]
    static class Initilization
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        { 
            SqlServerTypes.TestUtilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
