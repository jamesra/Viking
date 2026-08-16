using Geometry;
using SqlGeometryUtils;
using System;
using System.Diagnostics;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Actions
{
    internal class CreateNewLinkedLocationAction : IAction, IEquatable<CreateNewLinkedLocationAction>
    {
        /// <summary>
        /// ID of location on adjacent section we expect to link to
        /// </summary>
        public long ExistingLocID;
        private readonly IVolumeToSectionTransform Transform;

        /// <summary>
        /// The mosaic space polygon we want to commit to the database
        /// </summary>
        public readonly IShape2D NewMosaicShape;

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly IShape2D NewVolumeShape;

        public LocationAction Type => LocationAction.CREATELINKEDLOCATION;

        public Action Execute => OnExecute;

        /// <summary>
        /// Section to create the new linked location upon
        /// </summary>
        public int SectionNumber;

        public CreateNewLinkedLocationAction(long existingLocID, IShape2D newMosaicPolygon, IShape2D newVolumePolygon, int SectionNumber, IVolumeToSectionTransform? transform = null)
        {
            this.SectionNumber = SectionNumber;
            ExistingLocID = existingLocID;
            Transform = transform ?? AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

            NewMosaicShape = newMosaicPolygon;
            NewVolumeShape = newVolumePolygon ?? Transform.TryMapShapeSectionToVolume(newMosaicPolygon.ToSqlGeometry()).ToIShape2D();
        }

        public void OnExecute()
        {
            try
            {
                LocationObj existingLoc = Store.Locations[ExistingLocID];
                LocationObj newLoc = new(existingLoc.Parent,
                                                     NewMosaicShape,
                                                     NewVolumeShape,
                                                     SectionNumber,
                                                     NewMosaicShape.ShapeType.IsClosed() ? Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON : Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYLINE);

                LocationObj NewLocation = Store.Locations.Create(newLoc, [ExistingLocID]);
                Global.LastEditedAnnotationID = NewLocation.ID;
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("The chosen point is outside mappable volume space, location not created", "Recoverable Error");
            }
        }

        public bool Equals(IAction other)
        {
            CreateNewLinkedLocationAction other_obj = other as CreateNewLinkedLocationAction;
            if (object.ReferenceEquals(other_obj, null))
            {
                return false;
            }

            return Equals(other_obj);
        }

        public bool Equals(CreateNewLinkedLocationAction other)
        {
            if (object.ReferenceEquals(other, null))
            {
                return false;
            }

            return ExistingLocID == other.ExistingLocID && Type == other.Type && NewMosaicShape == other.NewMosaicShape;
        }
    }
}
