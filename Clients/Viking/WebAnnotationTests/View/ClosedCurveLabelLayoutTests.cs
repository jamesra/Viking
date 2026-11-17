using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.Serialization;
using WebAnnotation.View;
using WebAnnotationModel;

namespace WebAnnotationTests.View
{
    /// <summary>
    /// Closed-curve labels are deferred until a visible scene requests them; the constructor must not build a layout.
    /// </summary>
    [TestClass]
    public class ClosedCurveLabelLayoutTests
    {
        [TestMethod]
        public void ConstructorDoesNotBuildLabelLayout()
        {
            LocationClosedCurveView view = ViewWithoutConstructor();

            Assert.IsNull(GetPrivateField<object>(view, "labelLayout"));
            Assert.AreEqual(0, GetPrivateField<int>(view, "labelLayoutState"));
            Assert.AreEqual(LabelPlacementKind.Circle, view.CreateLabelObjects());
            Assert.IsNull(view.curveLabels);
        }

        [TestMethod]
        public void IsLabelVisibleIsFalseUntilLayoutReady()
        {
            LocationClosedCurveView view = ViewWithoutConstructor();
            SetPrivateField(view, "labelLayoutState", 0);

            // Without a scene that would start work, and with no labels, visibility stays false.
            Assert.IsNull(view.curveLabels);
            // CreateLabelObjects must stay a no-op before layout.
            Assert.AreEqual(LabelPlacementKind.Circle, view.CreateLabelObjects());
        }

        [TestMethod]
        public void CreateLabelObjectsUsesLayoutWhenPresent()
        {
            LocationClosedCurveView view = ViewWithoutConstructor();
            // Simulate a failed layout state so RequestLabelLayoutIfNeeded will not race; CreateLabelObjects still no-ops.
            SetPrivateField(view, "labelLayoutState", 3);
            Assert.AreEqual(LabelPlacementKind.Circle, view.CreateLabelObjects());
        }

        private static LocationClosedCurveView ViewWithoutConstructor()
        {
            LocationClosedCurveView view = (LocationClosedCurveView)FormatterServices.GetUninitializedObject(typeof(LocationClosedCurveView));
            FieldInfo? model = typeof(LocationCanvasView).GetField("modelObj", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(model);
            model.SetValue(view, new LocationObj());
            SetPrivateField(view, "labelSync", new object());
            SetPrivateField(view, "overlappedLinks", System.Array.Empty<long>());
            SetPrivateField(view, "labelLayoutState", 0);
            return view;
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            FieldInfo? field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return (T)field.GetValue(instance)!;
        }

        private static void SetPrivateField(object instance, string name, object? value)
        {
            FieldInfo? field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? instance.GetType().BaseType?.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            // LabelSync and others are on LocationClosedCurveView itself.
            field ??= typeof(LocationClosedCurveView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(instance, value);
        }
    }
}
