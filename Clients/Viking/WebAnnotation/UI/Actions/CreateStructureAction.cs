using Geometry;
using SqlGeometryUtils;
using System;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI.Commands;
using WebAnnotationModel;

namespace WebAnnotation.UI.Actions
{
    internal abstract class CreateStructureActionBase(int SectionNumber, IVolumeToSectionTransform? transform = null) : IAction
    {
        protected IVolumeToSectionTransform Transform = transform ?? AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

        public long TypeID; //The TypeID the action will use for the new structure.   

        public LocationAction Type => LocationAction.CREATESTRUCTURE;

        public Action Execute => OnExecute;

        public abstract bool Equals(IAction other);

        public readonly int SectionNumber = SectionNumber;

        public abstract void OnExecute();
    }

    /// <summary>
    /// Create a new structure with the specified shape
    /// </summary>
    internal class Create2DStructureAction : CreateStructureActionBase, IEquatable<Create2DStructureAction>
    {

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly GridPolygon NewVolumePolygon;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly GridPolygon NewSmoothVolumePolygon;


        public Create2DStructureAction(long StructureTypeID, GridPolygon newVolumePolygon, int SectionNumber, IVolumeToSectionTransform? transform = null) : base(SectionNumber, transform)
        {
            NewVolumePolygon = newVolumePolygon;
            TypeID = StructureTypeID;
        }

        public override void OnExecute()
        {
            StructureTypeObj TypeObj = Store.StructureTypes.GetObjectByID(TypeID, true);
            if (TypeObj is null)
            {
                //TODO: Prompt the user with a dialog/UI interface to choose the type
                throw new ArgumentException($"StructureTypeID {TypeID} not found when assigning type to structure");
            }

            GridPolygon mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolygon);

            StructureObj newStruct = new(TypeObj);

            LocationObj newLocation = new(newStruct,
                                                      SectionNumber,
                                                      LocationType.CURVEPOLYGON);


            newLocation.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

            if (TypeObj.Parent != null)
            {
                //Enqueue extra command to select a parent
                WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.CommandQueue.EnqueueCommand(typeof(LinkStructureToParentCommand), [WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent, newStruct, newLocation]);
            }

            WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.CommandQueue.EnqueueCommand(typeof(CreateNewStructureCommand), [WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent, newStruct, newLocation]);

        }

        public override bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Type != other.Type)
            {
                return false;
            }

            if (other is not Create2DStructureAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(Create2DStructureAction other) => NewVolumePolygon.Equals(other.NewVolumePolygon);
    }

    /// <summary>
    /// Create a new structure with the specified shape
    /// </summary>
    internal class Create1DStructureAction : CreateStructureActionBase, IEquatable<Create1DStructureAction>
    {
        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly GridPolyline NewVolumeShape;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly GridPolyline NewSmoothVolumeShape;


        public Create1DStructureAction(long StructureTypeID, GridPolyline newVolumeShape, int SectionNumber, IVolumeToSectionTransform? transform = null) : base(SectionNumber, transform)
        {
            NewVolumeShape = newVolumeShape;
            TypeID = StructureTypeID;

        }

        public override void OnExecute()
        {
            StructureTypeObj TypeObj = Store.StructureTypes.GetObjectByID(TypeID, true);
            if (TypeObj is null)
            {
                //TODO: Prompt the user with a dialog/UI interface to choose the type
                throw new ArgumentException($"StructureTypeID {TypeID} not found when assigning type to structure");
            }

            GridPolyline mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumeShape);

            StructureObj newStruct = new(TypeObj);

            LocationObj newLocation = new(newStruct,
                                                      SectionNumber,
                                                      LocationType.OPENCURVE)
            {
                Width = Global.DefaultClosedLineWidth
            };


            newLocation.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());

            if (TypeObj.Parent != null)
            {
                //Enqueue extra command to select a parent
                WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.CommandQueue.EnqueueCommand(typeof(LinkStructureToParentCommand), [WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent, newStruct, newLocation]);
            }

            WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.CommandQueue.EnqueueCommand(typeof(CreateNewStructureCommand), [WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent, newStruct, newLocation]);

        }

        public override bool Equals(IAction other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Type != other.Type)
            {
                return false;
            }

            if (other is not Create1DStructureAction other_action)
            {
                return false;
            }

            return Equals(other_action);
        }

        public bool Equals(Create1DStructureAction other) => NewVolumeShape.Equals(other.NewVolumeShape);
    }
}
