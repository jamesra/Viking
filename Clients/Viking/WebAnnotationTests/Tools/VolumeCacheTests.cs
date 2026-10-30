using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// The stored volume cache is always written under the volume's default transform, whatever transform is on screen, so
    /// server queries, OData and export see one space. See <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
    /// </summary>
    [TestClass]
    public class VolumeCacheTests
    {
        /// <summary>
        /// <see cref="VolumeViewModel.GetDefaultSectionToVolumeTransform"/> is the one helper that gives the default transform:
        /// the default group's .stos while another group is active, the active transform's own object while the default is
        /// active, and identity for a section the default has no entry for (the reference section) or a volume without it.
        /// </summary>
        [TestMethod]
        public void TheVolumeViewModelGivesTheDefaultTransformWhateverIsActive()
        {
            Volume volume = (Volume)FormatterServices.GetUninitializedObject(typeof(Volume));
            volume.Sections = [];
            volume.Transforms = new SortedList<string, SortedList<int, ITransform>>
            {
                ["Grid"] = new() { [5] = new Geometry.Transforms.IdentityTransform() },
                ["Rigid"] = new() { [5] = new Geometry.Transforms.IdentityTransform() },
            };
            volume.DefaultVolumeTransform = "Grid";
            VolumeViewModel model = new(volume);

            IVolumeToSectionTransform grid = model.GetDefaultSectionToVolumeTransform(5);
            Assert.AreSame(model.GetSectionToVolumeTransform(5), grid, "the default is active: the same object");

            model.ActiveVolumeTransform = "Rigid";
            Assert.AreNotSame(grid, model.GetSectionToVolumeTransform(5), "another transform is active");
            Assert.AreSame(grid, model.GetDefaultSectionToVolumeTransform(5), "the default is unchanged");

            IVolumeToSectionTransform reference = model.GetDefaultSectionToVolumeTransform(1);
            Assert.AreEqual(new Vector2(12, 34), reference.SectionToVolume(new Vector2(12, 34)), "no entry: identity");

            volume.DefaultVolumeTransform = "Not loaded";
            Assert.AreEqual(new Vector2(12, 34), model.GetDefaultSectionToVolumeTransform(5).SectionToVolume(new Vector2(12, 34)), "no such group: identity");
        }
    }
}
