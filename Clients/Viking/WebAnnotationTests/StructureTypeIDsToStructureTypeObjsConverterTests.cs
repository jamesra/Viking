using AnnotationService.Types;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.WPF.Converters;
using WebAnnotation.WPF.MockData;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="StructureTypeIDsToStructureTypeObjsConverter"/>, which the structure type forms bind to
    /// structure type IDs (a scalar ID, <c>long[]</c> root IDs, <c>ObservableCollection&lt;ulong&gt;</c> favorites)
    /// or to structure types that are already resolved. Only the inputs that do not reach the annotation store are
    /// covered through <c>Convert</c> here; the ID lists that do are covered through the ID parsing they share.
    /// </summary>
    [TestClass]
    public class StructureTypeIDsToStructureTypeObjsConverterTests
    {
        private static readonly StructureTypeIDsToStructureTypeObjsConverter Converter = new();
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private static object? Convert(object? value) => Converter.Convert(value!, typeof(object), null!, Culture);

        /// <summary>A type that is both a structure type and a collection of them, which ConvertBack refuses.</summary>
        private sealed class StructureTypeCollection : MockStructureType, IEnumerable<IStructureTypeReadOnly>
        {
            public IEnumerator<IStructureTypeReadOnly> GetEnumerator() { yield return this; }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [TestMethod]
        public void Convert_Null_ReturnsNull()
        {
            Assert.IsNull(Convert(null));
        }

        /// <summary>A value already of the target type is returned as is, even an ID list that would hit the store.</summary>
        [TestMethod]
        public void Convert_ValueOfTargetType_ReturnsSameInstance()
        {
            var ids = new List<long> { 1, 2 };
            Assert.AreSame(ids, Converter.Convert(ids, typeof(List<long>), null, Culture));
        }

        [TestMethod]
        public void Convert_MockStructureType_ReturnsSameInstance()
        {
            var type = new MockStructureType();
            Assert.AreSame(type, Convert(type));
        }

        [TestMethod]
        public void Convert_StructureTypeObj_ReturnsSameInstance()
        {
            var type = new StructureTypeObj(new StructureType { ID = 7, Code = "C" });
            Assert.AreSame(type, Convert(type));
        }

        [TestMethod]
        public void Convert_MockStructureTypes_ReturnsSameInstance()
        {
            var types = new MockStructureTypes { new MockStructureType() };
            Assert.AreSame(types, Convert(types));
        }

        [TestMethod]
        public void Convert_ListOfMockStructureType_ReturnsSameInstance()
        {
            var types = new List<MockStructureType> { new() };
            Assert.AreSame(types, Convert(types));
        }

        [TestMethod]
        public void Convert_ListOfStructureTypeInterface_ReturnsSameInstance()
        {
            var types = new List<IStructureTypeReadOnly> { new MockStructureType() };
            Assert.AreSame(types, Convert(types));
        }

        /// <summary>An empty array of structure types is still a structure type list, not an ID list.</summary>
        [TestMethod]
        public void Convert_EmptyStructureTypeArray_ReturnsSameInstance()
        {
            var types = new StructureTypeObj[0];
            Assert.AreSame(types, Convert(types));
        }

        /// <summary>Any other collection passes through unchanged rather than being read as IDs.</summary>
        [TestMethod]
        public void Convert_OtherCollections_ReturnSameInstance()
        {
            var untyped = new ArrayList { 1L, 2L };
            var objects = new List<object> { 1L };
            var doubles = new[] { 1.0 };
            Assert.AreSame(untyped, Convert(untyped));
            Assert.AreSame(objects, Convert(objects));
            Assert.AreSame(doubles, Convert(doubles));
        }

        /// <summary>A string is a collection of chars, so a textual ID passes through as the string.</summary>
        [TestMethod]
        public void Convert_String_ReturnsSameInstance()
        {
            const string id = "12";
            Assert.AreSame(id, Convert(id));
        }

        /// <summary>A value that is neither a structure type, a collection, nor convertible to an ID throws.</summary>
        [TestMethod]
        public void Convert_UnknownScalar_ThrowsInvalidCast()
        {
            Assert.ThrowsException<InvalidCastException>(() => Convert(new Version(1, 0)));
        }

        [TestMethod]
        public void ConvertBack_StructureType_ReturnsItsID()
        {
            var mock = new MockStructureType { ID = 42 };
            var obj = new StructureTypeObj(new StructureType { ID = 7, Code = "C" });
            Assert.AreEqual(42UL, Converter.ConvertBack(mock, typeof(object), null, Culture));
            Assert.AreEqual(7UL, Converter.ConvertBack(obj, typeof(object), null, Culture));
        }

        /// <summary>Collections of structure types and anything else are refused.</summary>
        [TestMethod]
        public void ConvertBack_NonStructureType_Throws()
        {
            Assert.ThrowsException<NotImplementedException>(() =>
                Converter.ConvertBack(new List<IStructureTypeReadOnly> { new MockStructureType() }, typeof(object), null, Culture));
            Assert.ThrowsException<NotImplementedException>(() =>
                Converter.ConvertBack(new ObservableCollection<StructureTypeObj>(), typeof(object), null, Culture));
            Assert.ThrowsException<NotImplementedException>(() =>
                Converter.ConvertBack(7L, typeof(object), null, Culture));
            Assert.ThrowsException<NotImplementedException>(() =>
                Converter.ConvertBack(new StructureTypeCollection(), typeof(object), null, Culture));
        }

        /// <summary>Null is refused before it is formatted into the message.</summary>
        [TestMethod]
        public void ConvertBack_Null_ThrowsNullReference()
        {
            Assert.ThrowsException<NullReferenceException>(() => Converter.ConvertBack(null!, typeof(object), null, Culture));
        }

        private static List<long>? IDs(object value) => StructureTypeIDsToStructureTypeObjsConverter.ToStructureTypeIDs(value);

        /// <summary>
        /// Root IDs bind as <c>long[]</c>; every element is kept in order. A list is checked too, because the runtime
        /// also reports a <c>long[]</c> as <c>IEnumerable&lt;ulong&gt;</c>, which would hide a missing long case.
        /// </summary>
        [TestMethod]
        public void ToStructureTypeIDs_Longs_KeepsEveryID()
        {
            Prop.ForAll<long[]>(ids => IDs(ids).SequenceEqual(ids) && IDs(new List<long>(ids)).SequenceEqual(ids))
                .QuickCheckThrowOnFailure();
        }

        /// <summary>Favorite IDs bind as <c>ObservableCollection&lt;ulong&gt;</c>; each is reinterpreted as a long key.</summary>
        [TestMethod]
        public void ToStructureTypeIDs_Ulongs_ReinterpretsEachID()
        {
            Prop.ForAll<ulong[]>(ids =>
                    IDs(new ObservableCollection<ulong>(ids)).SequenceEqual(ids.Select(i => unchecked((long)i))))
                .QuickCheckThrowOnFailure();
            CollectionAssert.AreEqual(new long[] { -1 }, IDs(new ObservableCollection<ulong> { ulong.MaxValue }));
        }

        /// <summary>
        /// Int IDs widen to longs. Before this was pinned the int branch cast an empty <c>List&lt;long&gt;</c> to
        /// <c>IEnumerable&lt;int&gt;</c> and threw <see cref="InvalidCastException"/> for every input.
        /// </summary>
        [TestMethod]
        public void ToStructureTypeIDs_Ints_WidenEachID()
        {
            Prop.ForAll<int[]>(ids => IDs(new List<int>(ids)).SequenceEqual(ids.Select(i => (long)i)))
                .QuickCheckThrowOnFailure();
            CollectionAssert.AreEqual(new long[] { int.MinValue, 3 }, IDs(new[] { int.MinValue, 3 }));
        }

        /// <summary>A string collection parses each entry; a null entry reads as 0 and a non-number throws.</summary>
        [TestMethod]
        public void ToStructureTypeIDs_StringCollection_ParsesEachEntry()
        {
            var strings = new StringCollection { "12", "-4", null! };
            CollectionAssert.AreEqual(new long[] { 12, -4, 0 }, IDs(strings));
            Assert.ThrowsException<FormatException>(() => IDs(new StringCollection { "abc" }));
            CollectionAssert.AreEqual(new long[0], IDs(new StringCollection()));
        }

        /// <summary>Structure types, other collections and scalars are not ID lists.</summary>
        [TestMethod]
        public void ToStructureTypeIDs_NotAnIDCollection_ReturnsNull()
        {
            Assert.IsNull(IDs(5L));
            Assert.IsNull(IDs("12"));
            Assert.IsNull(IDs(new ArrayList { 1L }));
            Assert.IsNull(IDs(new List<object> { 1L }));
            Assert.IsNull(IDs(new[] { 1.0 }));
            Assert.IsNull(IDs(new List<MockStructureType>()));
        }
    }
}
