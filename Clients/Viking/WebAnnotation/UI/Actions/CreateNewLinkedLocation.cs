using Geometry;
using SqlGeometryUtils;
using System;
using System.Diagnostics;
using Viking.VolumeModel;
using WebAnnotationModel;

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

        /// <summary>
        /// Maps a pen-drawn volume shape to the section and wraps it in an action that creates a location linked to <paramref name="existingLocID"/>.
        /// </summary>
        /// <param name="existingLocID">Location on the adjacent section the new location links to.</param>
        /// <param name="volumeShape">A <see cref="Polygon"/> for a closed stroke or a <see cref="Polyline"/> for an open one, in volume space.</param>
        /// <param name="sectionNumber">Section the new location is created on.</param>
        /// <param name="transform">Mapping between that section and the volume.</param>
        /// <returns>The action, or null (with a trace line) when any vertex of the shape cannot be mapped to the section,
        /// so no linked-location choice is offered whose execution would fail.</returns>
        internal static CreateNewLinkedLocationAction TryCreate(long existingLocID, IShape2D volumeShape, int sectionNumber, IVolumeToSectionTransform transform)
        {
            IShape2D mosaicShape = volumeShape switch
            {
                Polygon polygon => transform.TryMapShapeVolumeToSection(polygon),
                Polyline line => transform.TryMapShapeVolumeToSection(line),
                _ => throw new ArgumentException("Expected a Polygon or Polyline, got " + volumeShape?.GetType().Name, nameof(volumeShape))
            };

            if (mosaicShape is null)
            {
                Trace.WriteLine("CreateNewLinkedLocationAction: new shape crosses an unmapped region of section " + sectionNumber + "; no linked location offered for location " + existingLocID + ".", "WebAnnotation");
                return null;
            }

            return new CreateNewLinkedLocationAction(existingLocID, mosaicShape, volumeShape, sectionNumber, transform);
        }

        public void OnExecute()
        {
            try
            {
                LocationObj existingLoc = Store.Locations[ExistingLocID];
                LocationObj newLoc = new(existingLoc.Parent,
                                                     NewMosaicShape.ToSqlGeometry(),
                                                     NewVolumeShape.ToSqlGeometry(),
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
