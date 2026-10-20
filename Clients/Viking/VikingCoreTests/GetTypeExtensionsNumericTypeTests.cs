using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="GetTypeExtensions.IsNumericType"/>, which <see cref="ListViewColumnSorter"/> uses
    /// to choose decimal comparison versus lexical sort for ObjectListView columns.
    /// </summary>
    [TestClass]
    public class GetTypeExtensionsNumericTypeTests
    {
        private static readonly Type[] PrimitiveNumericTypes =
        {
            typeof(byte),
            typeof(sbyte),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(double),
            typeof(float),
            typeof(decimal),
        };

        private static readonly Type[] NonNumericTypes =
        {
            typeof(string),
            typeof(bool),
            typeof(DateTime),
            typeof(object),
            typeof(void),
            typeof(DayOfWeek),
            typeof(Uri),
        };

        private static bool ExpectedIsNumeric(Type? t)
        {
            if (t is null)
                return false;

            if (GetTypeExtensions.NumericTypes.Contains(t))
                return true;

            Type? underlying = Nullable.GetUnderlyingType(t);
            return underlying != null && GetTypeExtensions.NumericTypes.Contains(underlying);
        }

        [TestMethod]
        public void IsNumericType_NullType_ReturnsFalse()
        {
            Assert.IsFalse(((Type?)null).IsNumericType());
        }

        [TestMethod]
        public void IsNumericType_PrimitiveNumericTypes_ReturnTrue()
        {
            foreach (Type t in PrimitiveNumericTypes)
            {
                Assert.IsTrue(t.IsNumericType(), t.FullName);
            }
        }

        [TestMethod]
        public void IsNumericType_NullableNumericTypes_ReturnTrue()
        {
            foreach (Type t in PrimitiveNumericTypes)
            {
                Type nullable = typeof(Nullable<>).MakeGenericType(t);
                Assert.IsTrue(nullable.IsNumericType(), nullable.FullName);
            }
        }

        [TestMethod]
        public void IsNumericType_NonNumericTypes_ReturnFalse()
        {
            foreach (Type t in NonNumericTypes)
            {
                Assert.IsFalse(t.IsNumericType(), t.FullName);
            }
        }

        [TestMethod]
        public void IsNumericType_NullableNonNumericTypes_ReturnFalse()
        {
            Type[] nullableNonNumeric =
            {
                typeof(bool?),
                typeof(DateTime?),
                typeof(DayOfWeek?),
            };

            foreach (Type nullable in nullableNonNumeric)
            {
                Assert.IsFalse(nullable.IsNumericType(), nullable.FullName);
            }
        }

        [TestMethod]
        public void IsNumericType_CatalogTypes_MatchNumericTypesSet()
        {
            Type[] catalog =
            {
                typeof(byte),
                typeof(int?),
                typeof(string),
                typeof(decimal),
                typeof(bool),
                typeof(long?),
                typeof(DayOfWeek),
                typeof(float),
                typeof(DateTime?),
                typeof(double?),
            };

            var typeGen = Gen.Elements(catalog);

            Prop.ForAll(Arb.From(typeGen), t =>
            {
                Assert.AreEqual(ExpectedIsNumeric(t), t.IsNumericType());
            }).QuickCheckThrowOnFailure();
        }
    }
}
