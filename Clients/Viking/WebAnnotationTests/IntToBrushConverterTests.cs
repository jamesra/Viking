using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Windows.Media;
using WebAnnotation.WPF.Converters;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="IntToBrushConverter"/>, which binds the packed ARGB color stored on a structure type (a signed
    /// <c>int</c> in the WCF data, so colors with alpha 0x80 or higher are negative) to WPF brushes and colors.
    /// </summary>
    [TestClass]
    public class IntToBrushConverterTests
    {
        private static readonly IntToBrushConverter Converter = new();
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>Distinct channel values so a swapped or dropped channel changes the result.</summary>
        private const uint Argb = 0x80123456u;

        private static readonly Color ArgbColor = Color.FromArgb(0x80, 0x12, 0x34, 0x56);

        /// <summary>A uint converts to a Color with each byte in its own channel: alpha is the high byte.</summary>
        [TestMethod]
        public void Convert_Uint_ToColor_SplitsArgbChannels()
        {
            object result = Converter.Convert(Argb, typeof(Color), null, Culture);
            Assert.AreEqual(ArgbColor, result);
        }

        /// <summary>A Brush target yields a SolidColorBrush of the same color.</summary>
        [TestMethod]
        public void Convert_Uint_ToBrush_ReturnsSolidColorBrushOfColor()
        {
            object result = Converter.Convert(Argb, typeof(Brush), null, Culture);
            Assert.IsInstanceOfType<SolidColorBrush>(result);
            SolidColorBrush brush = (SolidColorBrush)result;
            Assert.AreEqual(ArgbColor, brush.Color);
        }

        /// <summary>A target that Brush is assignable to (object) also gets the brush, because Brush is tested first.</summary>
        [TestMethod]
        public void Convert_ObjectTarget_ReturnsBrush()
        {
            object result = Converter.Convert(Argb, typeof(object), null, Culture);
            Assert.IsInstanceOfType<SolidColorBrush>(result);
        }

        /// <summary>A negative int (alpha 0x80 or higher sets the sign bit) reinterprets as the same uint bits.</summary>
        [TestMethod]
        public void Convert_NegativeInt_ToColor_KeepsHighAlphaBit()
        {
            int packed = unchecked((int)0xFF102030u);
            Assert.IsTrue(packed < 0);
            object result = Converter.Convert(packed, typeof(Color), null, Culture);
            Assert.AreEqual(Color.FromArgb(0xFF, 0x10, 0x20, 0x30), result);
        }

        /// <summary>A positive int (alpha below 0x80) converts the same way.</summary>
        [TestMethod]
        public void Convert_PositiveInt_ToColor_SplitsArgbChannels()
        {
            object result = Converter.Convert(0x7F010203, typeof(Color), null, Culture);
            Assert.AreEqual(Color.FromArgb(0x7F, 0x01, 0x02, 0x03), result);
        }

        /// <summary>A string is parsed with <c>Convert.ToUInt32</c> (decimal) and converted like the uint.</summary>
        [TestMethod]
        public void Convert_DecimalString_ToColor_ParsesAsUint()
        {
            object result = Converter.Convert(Argb.ToString(CultureInfo.InvariantCulture), typeof(Color), null, Culture);
            Assert.AreEqual(ArgbColor, result);
        }

        /// <summary>An unparseable string surfaces the framework FormatException.</summary>
        [TestMethod]
        public void Convert_NonNumericString_ThrowsFormatException()
        {
            Assert.ThrowsExactly<FormatException>(() => Converter.Convert("red", typeof(Color), null, Culture));
        }

        /// <summary>A value that is not a string, int, or uint is rejected with NotImplementedException naming the value.</summary>
        [TestMethod]
        public void Convert_UnsupportedValueType_ThrowsNotImplemented()
        {
            NotImplementedException ex = Assert.ThrowsExactly<NotImplementedException>(
                () => Converter.Convert(1.5, typeof(Color), null, Culture));
            StringAssert.Contains(ex.Message, "1.5");
        }

        /// <summary>
        /// Pins a defect: a null value (for example a cleared binding source) throws NullReferenceException from
        /// building the error message, not the intended NotImplementedException.
        /// </summary>
        [TestMethod]
        public void Convert_NullValue_ThrowsNullReference()
        {
            Assert.ThrowsExactly<NullReferenceException>(() => Converter.Convert(null, typeof(Color), null, Culture));
        }

        /// <summary>A target type neither Brush nor Color is assignable to is rejected.</summary>
        [TestMethod]
        public void Convert_UnsupportedTargetType_ThrowsNotImplemented()
        {
            Assert.ThrowsExactly<NotImplementedException>(() => Converter.Convert(Argb, typeof(string), null, Culture));
        }

        /// <summary>Color converts back to a uint with alpha in the high byte.</summary>
        [TestMethod]
        public void ConvertBack_Color_ToUint_PacksArgb()
        {
            object result = Converter.ConvertBack(ArgbColor, typeof(uint), null, Culture);
            Assert.IsInstanceOfType<uint>(result);
            Assert.AreEqual(Argb, (uint)result);
        }

        /// <summary>Color converts back to an int that is negative when alpha is 0x80 or higher.</summary>
        [TestMethod]
        public void ConvertBack_Color_ToInt_IsNegativeForHighAlpha()
        {
            object result = Converter.ConvertBack(Color.FromArgb(0xFF, 0x10, 0x20, 0x30), typeof(int), null, Culture);
            Assert.IsInstanceOfType<int>(result);
            Assert.AreEqual(unchecked((int)0xFF102030u), (int)result);
        }

        /// <summary>Alpha below 0x80 gives a positive int.</summary>
        [TestMethod]
        public void ConvertBack_Color_ToInt_IsPositiveForLowAlpha()
        {
            object result = Converter.ConvertBack(Color.FromArgb(0x7F, 0x01, 0x02, 0x03), typeof(int), null, Culture);
            Assert.IsInstanceOfType<int>(result);
            Assert.AreEqual(0x7F010203, (int)result);
        }

        /// <summary>Convert then ConvertBack returns the original packed value for a range of alphas and channels.</summary>
        [TestMethod]
        public void RoundTrip_IntToColorToInt_ReturnsOriginal()
        {
            foreach (uint packed in new[] { 0u, 0x00000001u, 0x7FFFFFFFu, 0x80000000u, 0xFF000000u, 0xFFFFFFFFu, Argb, 0xFF8040C0u })
            {
                int asInt = unchecked((int)packed);
                object color = Converter.Convert(asInt, typeof(Color), null, Culture);
                Assert.AreEqual(asInt, Converter.ConvertBack(color, typeof(int), null, Culture), $"int 0x{packed:X8}");
                Assert.AreEqual(packed, Converter.ConvertBack(color, typeof(uint), null, Culture), $"uint 0x{packed:X8}");
            }
        }

        /// <summary>A target type that is neither int nor uint is rejected.</summary>
        [TestMethod]
        public void ConvertBack_UnsupportedTargetType_ThrowsNotImplemented()
        {
            Assert.ThrowsExactly<NotImplementedException>(() => Converter.ConvertBack(ArgbColor, typeof(string), null, Culture));
        }

        /// <summary>A brush (not a Color) is not converted back.</summary>
        [TestMethod]
        public void ConvertBack_Brush_ThrowsNotImplemented()
        {
            Assert.ThrowsExactly<NotImplementedException>(
                () => Converter.ConvertBack(new SolidColorBrush(ArgbColor), typeof(int), null, Culture));
        }

        /// <summary>
        /// Pins a defect: a null value (a cleared ColorPicker.SelectedColor is a null Color?) makes ConvertBack throw
        /// instead of returning Binding.DoNothing or a default.
        /// </summary>
        [TestMethod]
        public void ConvertBack_NullValue_ThrowsNotImplemented()
        {
            Assert.ThrowsExactly<NotImplementedException>(() => Converter.ConvertBack(null, typeof(int), null, Culture));
        }
    }
}
