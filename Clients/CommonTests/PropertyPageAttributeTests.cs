using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins <see cref="PropertyPageAttribute"/> constructors and <see cref="PropertyPageAttribute.ResolveTargetType"/>,
    /// which <see cref="Viking.UI.WPF.PropertyPages.PropertyPageRegistry"/> and NGVV
    /// <c>ExtensionManager</c> use to match property pages to annotated types.
    /// </summary>
    [TestClass]
    public class PropertyPageAttributeTests
    {
        [TestMethod]
        public void TypeConstructor_NullType_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new PropertyPageAttribute(null!));
        }

        [TestMethod]
        public void TypeConstructor_OmittedPriority_IsOne()
        {
            var attribute = new PropertyPageAttribute(typeof(string));
            Assert.AreEqual(1, attribute.Priority);
            Assert.AreEqual(typeof(string), attribute.TargetType);
            Assert.IsNull(attribute.TargetTypeName);
        }

        [TestMethod]
        public void TypeConstructor_ExplicitPriority_IsStored()
        {
            var attribute = new PropertyPageAttribute(typeof(int), priority: 7);
            Assert.AreEqual(7, attribute.Priority);
            Assert.AreEqual(typeof(int), attribute.TargetType);
        }

        [TestMethod]
        public void NameConstructor_NullName_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new PropertyPageAttribute((string)null!));
        }

        [TestMethod]
        public void NameConstructor_DefaultPriority_IsOne()
        {
            const string name = "System.String, mscorlib";
            var attribute = new PropertyPageAttribute(name);
            Assert.AreEqual(1, attribute.Priority);
            Assert.AreEqual(name, attribute.TargetTypeName);
            Assert.IsNull(attribute.TargetType);
        }

        [TestMethod]
        public void ResolveTargetType_TypeConstructor_ReturnsTargetType()
        {
            var attribute = new PropertyPageAttribute(typeof(decimal), priority: 2);
            Assert.AreEqual(typeof(decimal), attribute.ResolveTargetType());
        }

        [TestMethod]
        public void ResolveTargetType_AssemblyQualifiedName_ResolvesType()
        {
            string name = typeof(Uri).AssemblyQualifiedName!;
            var attribute = new PropertyPageAttribute(name, priority: 3);
            Assert.AreEqual(typeof(Uri), attribute.ResolveTargetType());
        }

        [TestMethod]
        public void ResolveTargetType_UnknownName_ReturnsNull()
        {
            var attribute = new PropertyPageAttribute("Not.A.Real.Type, NoSuchAssembly", priority: 0);
            Assert.IsNull(attribute.ResolveTargetType());
        }

        [TestMethod]
        public void ResolveTargetType_WhitespaceName_ReturnsNull()
        {
            var attribute = new PropertyPageAttribute("   ", priority: 0);
            Assert.IsNull(attribute.ResolveTargetType());
        }

        [TestMethod]
        public void ResolveTargetType_TypeConstructorIgnoresTargetTypeName()
        {
            var attribute = new PropertyPageAttribute(typeof(short), priority: 4);
            Assert.AreEqual(typeof(short), attribute.ResolveTargetType());
        }

        [TestMethod]
        public void ResolveTargetType_TypeConstructor_AlwaysReturnsSameReference()
        {
            Prop.ForAll(
                Gen.Elements(typeof(byte), typeof(long), typeof(double), typeof(bool)).ToArbitrary(),
                type =>
                {
                    var attribute = new PropertyPageAttribute(type);
                    return ReferenceEquals(type, attribute.ResolveTargetType());
                }).QuickCheckThrowOnFailure();
        }
    }
}
