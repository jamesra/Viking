using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="ExtensionManager.RegisterPropertyPages"/>, which Jotunn uses to show Viking's property sheets
    /// without loading Viking's extension modules.
    /// </summary>
    [TestClass]
    public class ExtensionManagerPropertyPageTests
    {
        public sealed class SheetTarget
        {
        }

        public sealed class DerivedSheetTarget
        {
        }

        [PropertyPage(typeof(SheetTarget))]
        public sealed class SheetTargetPage
        {
        }

        [TestMethod]
        public void RegisteringAnAssemblyAddsItsPagesOnce()
        {
            ExtensionManager.RegisterPropertyPages(typeof(ExtensionManagerPropertyPageTests).Assembly);
            ExtensionManager.RegisterPropertyPages(typeof(ExtensionManagerPropertyPageTests).Assembly);

            CollectionAssert.AreEqual(new[] { typeof(SheetTargetPage) }, ExtensionManager.GetPropertyPages(typeof(SheetTarget)).ToArray());
            Assert.AreEqual(0, ExtensionManager.GetPropertyPages(typeof(DerivedSheetTarget)).Length);
        }
    }
}
