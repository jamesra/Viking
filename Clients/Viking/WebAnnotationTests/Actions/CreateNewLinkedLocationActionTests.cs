using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Viking.VolumeModel;
using WebAnnotation.UI.Actions;

namespace WebAnnotationTests.Actions
{
    /// <summary>
    /// A pen stroke over an adjacent section's circle offers a linked location through <see cref="CreateNewLinkedLocationAction.TryCreate"/>.
    /// A stroke that reaches past the section mapping must offer nothing, instead of an action whose null section shape
    /// throws a NullReferenceException when chosen. Reuses the bounded transform from <see cref="ChangeTypeActionTests"/>.
    /// </summary>
    [TestClass]
    public class CreateNewLinkedLocationActionTests
    {
        [TestMethod]
        public void UnmappablePolygonOffersNoAction() =>
            Assert.IsNull(CreateNewLinkedLocationAction.TryCreate(7, new Polygon(ChangeTypeActionTests.UnmappableRing()), 2, ChangeTypeActionTests.CreateBoundedTransform()));

        /// <summary>
        /// An open stroke that starts over the circle and runs off the mapped area.
        /// </summary>
        [TestMethod]
        public void PartlyUnmappablePolylineOffersNoAction() =>
            Assert.IsNull(CreateNewLinkedLocationAction.TryCreate(7, new Polyline(new Vector2[] { new(10, 10), new(150, 20), new(5000, 120) }, false), 2, ChangeTypeActionTests.CreateBoundedTransform()));

        [TestMethod]
        public void MappablePolygonOffersActionWithSectionShape()
        {
            IVolumeToSectionTransform mapping = ChangeTypeActionTests.CreateBoundedTransform();
            Polygon volume = new([new(10, 10), new(150, 20), new(180, 120), new(15, 140), new(10, 10)]);

            CreateNewLinkedLocationAction action = CreateNewLinkedLocationAction.TryCreate(7, volume, 2, mapping);

            Assert.IsNotNull(action);
            Assert.AreEqual(7, action.ExistingLocID);
            Assert.AreEqual(2, action.SectionNumber);
            Assert.AreSame(volume, action.NewVolumeShape);
            CollectionAssert.AreEqual(mapping.TryMapShapeVolumeToSection(volume).ExteriorRing, ((Polygon)action.NewMosaicShape).ExteriorRing);
        }

        [TestMethod]
        public void MappablePolylineOffersActionWithSectionShape()
        {
            IVolumeToSectionTransform mapping = ChangeTypeActionTests.CreateBoundedTransform();
            Polyline volume = new(new Vector2[] { new(10, 10), new(150, 20), new(180, 120) }, false);

            CreateNewLinkedLocationAction action = CreateNewLinkedLocationAction.TryCreate(7, volume, 2, mapping);

            Assert.IsNotNull(action);
            Assert.AreSame(volume, action.NewVolumeShape);
            CollectionAssert.AreEqual(mapping.TryMapShapeVolumeToSection(volume).Points.ToArray(), ((Polyline)action.NewMosaicShape).Points.ToArray());
        }
    }
}
