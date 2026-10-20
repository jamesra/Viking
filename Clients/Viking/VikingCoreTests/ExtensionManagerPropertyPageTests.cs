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

        public sealed class CountingMenuProvider : IProvideContextMenus
        {
            public System.Windows.Forms.ContextMenuStrip BuildMenuFor(object Obj, System.Windows.Forms.ContextMenuStrip Menu) => Menu;

            public System.Windows.Forms.ContextMenuStrip BuildMenuFor(System.Type ObjType, System.Windows.Forms.ContextMenuStrip Menu) => Menu;
        }

        [TestMethod]
        public void RegisteringAnAssemblyAddsItsContextMenuProvidersOnce()
        {
            ExtensionManager.RegisterContextMenuProviders(typeof(ExtensionManagerPropertyPageTests).Assembly);
            ExtensionManager.RegisterContextMenuProviders(typeof(ExtensionManagerPropertyPageTests).Assembly);

            Assert.AreEqual(1, ExtensionManager.CreateContextMenuProviders().Count(p => p is CountingMenuProvider));
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
