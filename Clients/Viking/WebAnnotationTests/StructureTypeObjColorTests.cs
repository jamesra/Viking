using Viking.AnnotationServiceTypes.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins that assigning <see cref="StructureTypeObj.Color"/> its current value is a no-op. The structure type
    /// editor's color picker writes the same color back (for example when its Advanced view opens), so a same-value
    /// assignment must not raise change events or mark the type for saving. The color is stored as a signed
    /// <c>int</c> on the wire, so colors with alpha 0x80 or higher are negative there.
    /// </summary>
    [TestClass]
    public class StructureTypeObjColorTests
    {
        private static StructureTypeObj NewType(uint color)
        {
            StructureTypeObj type = new(1) { Code = "C", Color = color };
            type.DBAction = DBACTION.NONE;
            return type;
        }

        private static List<string> RecordEvents(StructureTypeObj obj)
        {
            List<string> events = [];
            obj.PropertyChanging += (s, e) => events.Add("Changing:" + e.PropertyName);
            obj.PropertyChanged += (s, e) => events.Add("Changed:" + e.PropertyName);
            return events;
        }

        /// <summary>
        /// Same value is a no-op across alpha values, including opaque colors (alpha 0xFF, negative as <c>int</c>),
        /// alpha exactly 0x80 (the sign bit boundary), and alpha below 0x80.
        /// </summary>
        [DataTestMethod]
        [DataRow(0xFFFF8000u, DisplayName = "Opaque orange")]
        [DataRow(0xFFFFFFFFu, DisplayName = "Opaque white (int -1)")]
        [DataRow(0x800000FFu, DisplayName = "Alpha 0x80 (int.MinValue + 255)")]
        [DataRow(0x7F00FF00u, DisplayName = "Alpha 0x7F (positive int)")]
        [DataRow(0x00000000u, DisplayName = "Zero")]
        public void AssigningSameColor_RaisesNoEvents_AndLeavesDBActionNone(uint color)
        {
            StructureTypeObj obj = NewType(color);
            List<string> events = RecordEvents(obj);

            obj.Color = color;

            Assert.AreEqual(0, events.Count, "Events: " + string.Join(", ", events));
            Assert.AreEqual(DBACTION.NONE, obj.DBAction);
            Assert.AreEqual(color, obj.Color);
        }

        /// <summary>A different color is stored, raises Changing/Changed for Color, and marks the type for update.</summary>
        [DataTestMethod]
        [DataRow(0xFFFF8000u, 0xFF00FF00u, DisplayName = "Opaque to opaque")]
        [DataRow(0x7F00FF00u, 0xFF00FF00u, DisplayName = "Positive int to negative int")]
        [DataRow(0xFFFF8000u, 0x7FFF8000u, DisplayName = "Alpha change only")]
        public void AssigningDifferentColor_RaisesEvents_AndMarksUpdate(uint original, uint updated)
        {
            StructureTypeObj obj = NewType(original);
            List<string> events = RecordEvents(obj);

            obj.Color = updated;

            Assert.AreEqual(updated, obj.Color);
            Assert.AreEqual(DBACTION.UPDATE, obj.DBAction);
            CollectionAssert.Contains(events, "Changing:Color");
            CollectionAssert.Contains(events, "Changed:Color");
        }
    }
}
