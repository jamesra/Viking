using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using AnnotationService.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using AnnotationGlobal = WebAnnotation.Global;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the location right-click menu's <c>Change Shape</c> submenu: it offers no <c>Curve</c> item, because
    /// converting a location to an open curve is not implemented (decision D-ChangeShapeCurveStub), while the
    /// working <c>Circle</c> and <c>Convert to Polygon</c> conversions stay.
    /// </summary>
    [TestClass]
    public class ConvertShapeMenuTests
    {
        /// <summary>
        /// Exposes the protected menu builder. Created without running a constructor, so no annotation store or server
        /// is needed; only <see cref="Location_ViewModelBase.modelObj"/> is set.
        /// </summary>
        private sealed class MenuProbe : Location_ViewModelBase
        {
            private MenuProbe() : base(0)
            {
            }

            public static MenuProbe For(LocationType type)
            {
                MenuProbe probe = (MenuProbe)FormatterServices.GetUninitializedObject(typeof(MenuProbe));
                FieldInfo? model = typeof(Location_ViewModelBase).GetField(nameof(modelObj), BindingFlags.Instance | BindingFlags.Public);
                Assert.IsNotNull(model);
                model.SetValue(probe, new LocationObj(new Location { ID = 1, TypeCode = (short)type, Section = 1 }));
                return probe;
            }

            public ToolStripMenuItem ChangeShapeMenu()
            {
                ContextMenuStrip menu = new();
                _AddConvertShapeMenus(menu);
                return menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Change Shape");
            }
        }

        private static readonly FieldInfo SegmentationAvailableField =
            typeof(AnnotationGlobal).GetField("_isSegmentationServiceAvailable", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(AnnotationGlobal), "_isSegmentationServiceAvailable");

        private object? _previousSegmentationAvailable;

        /// <summary>
        /// Reading <see cref="AnnotationGlobal"/> runs its type initializer, which needs a volume; the cached
        /// segmentation availability is saved so each test can fix it without asking a service.
        /// </summary>
        [TestInitialize]
        public void SaveSegmentationAvailability()
        {
            SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable();
            _previousSegmentationAvailable = SegmentationAvailableField.GetValue(null);
        }

        [TestCleanup]
        public void RestoreSegmentationAvailability() => SegmentationAvailableField.SetValue(null, _previousSegmentationAvailable);

        /// <summary>
        /// For every location type, with and without a segmentation service, <c>Change Shape</c> has no curve
        /// conversion, and offers <c>Circle</c> exactly when the location is not already a circle.
        /// </summary>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ChangeShapeOffersNoCurveAndKeepsCircle(bool segmentationAvailable)
        {
            SegmentationAvailableField.SetValue(null, (bool?)segmentationAvailable);

            foreach (LocationType type in Enum.GetValues(typeof(LocationType)))
            {
                ToolStripItem[] items = MenuProbe.For(type).ChangeShapeMenu().DropDownItems.Cast<ToolStripItem>().ToArray();

                Assert.IsFalse(items.Any(i => i.Text == "Curve"), $"{type}: no Curve item");
                LocationType[] targets = items.Select(i => i.Tag).OfType<LocationType>().ToArray();
                CollectionAssert.AreEqual(type == LocationType.CIRCLE ? Array.Empty<LocationType>() : new[] { LocationType.CIRCLE },
                    targets, $"{type}: the only shape conversion offered is Circle, and not to a circle");
                Assert.AreEqual(type == LocationType.CIRCLE, items.Any(i => i.Text == "Convert to Polygon"), $"{type}: Convert to Polygon is for circles");
            }
        }
    }
}
