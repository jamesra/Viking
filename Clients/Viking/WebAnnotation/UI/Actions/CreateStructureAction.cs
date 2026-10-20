using Geometry;
using SqlGeometryUtils;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotation.UI.Commands;

namespace WebAnnotation.UI.Actions
{
    internal abstract class CreateStructureActionBase(int SectionNumber, IVolumeToSectionTransform? transform = null) : IAction
    {
        protected IVolumeToSectionTransform Transform = transform ?? AnnotationOverlay.ActiveSectionToVolumeTransform;

        public long TypeID; //The TypeID the action will use for the new structure.   

        public LocationAction Type => LocationAction.CREATESTRUCTURE;

        public Action Execute => OnExecute;

        public abstract bool Equals(IAction other);

        public readonly int SectionNumber = SectionNumber;

        /// <summary>
        /// Maps the drawn volume shape to the section and queues creation of the new structure and its location.
        /// Creates nothing, and writes a trace line, when any vertex of the shape cannot be mapped to the section.
        /// </summary>
        public abstract void OnExecute();

        /// <summary>
        /// Set by a host without Viking's command queue (Jotunn) to save a structure a pen action created, asking for its
        /// parent first when its type needs one. Viking leaves it null and queues its commands.
        /// </summary>
        public static Func<StructureObj, LocationObj, Task>? CommitHandler { get; set; }

        protected static async Task CommitNewStructure(StructureTypeObj typeObj, StructureObj newStruct, LocationObj newLocation)
        {
            if (CommitHandler != null)
            {
                await CommitHandler(newStruct, newLocation);
                return;
            }

            if (typeObj.Parent != null)
            {
                AnnotationOverlay.CurrentOverlay.Parent.CommandQueue.EnqueueCommand(typeof(LinkStructureToParentCommand), [AnnotationOverlay.CurrentOverlay.Parent, newStruct, newLocation]);
            }

            AnnotationOverlay.CurrentOverlay.Parent.CommandQueue.EnqueueCommand(typeof(CreateNewStructureCommand), [AnnotationOverlay.CurrentOverlay.Parent, newStruct, newLocation]);
        }
    }

    /// <summary>
    /// Create a new structure with the specified shape
    /// </summary>
    internal class Create2DStructureAction : CreateStructureActionBase, IEquatable<Create2DStructureAction>
    {

        /// <summary>
        /// The volume space polygon we want to add to the location
        /// </summary>
        public readonly Polygon NewVolumePolygon;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly Polygon NewSmoothVolumePolygon;


        public Create2DStructureAction(long StructureTypeID, Polygon newVolumePolygon, int SectionNumber, IVolumeToSectionTransform? transform = null) : base(SectionNumber, transform)
        {
            NewVolumePolygon = newVolumePolygon;
            TypeID = StructureTypeID;
        }

        public override void OnExecute() => _ = OnExecuteAsync();

        async Task OnExecuteAsync()
        {
            if (!Store.StructureTypes.TryGetObjectByID(TypeID, out StructureTypeObj TypeObj) || TypeObj is null)
            {
                //TODO: Prompt the user with a dialog/UI interface to choose the type
                throw new ArgumentException($"StructureTypeID {TypeID} not found when assigning type to structure");
            }

            Polygon mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumePolygon);
            if (mosaic_polygon is null)
            {
                Trace.WriteLine("Create2DStructureAction: drawn polygon crosses an unmapped region of section " + SectionNumber + "; no structure created.", "WebAnnotation");
                return;
            }

            StructureObj newStruct = new(TypeObj);

            LocationObj newLocation = new(newStruct,
                                                      SectionNumber,
                                                      LocationType.CURVEPOLYGON);


            newLocation.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());
            await CommitNewStructure(TypeObj, newStruct, newLocation);
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
        public readonly Polyline NewVolumeShape;

        /// <summary>
        /// The volume space polygon after smoothing
        /// </summary>
        public readonly Polyline NewSmoothVolumeShape;


        public Create1DStructureAction(long StructureTypeID, Polyline newVolumeShape, int SectionNumber, IVolumeToSectionTransform? transform = null) : base(SectionNumber, transform)
        {
            NewVolumeShape = newVolumeShape;
            TypeID = StructureTypeID;

        }

        public override void OnExecute() => _ = OnExecuteAsync();

        async Task OnExecuteAsync()
        {
            if (!Store.StructureTypes.TryGetObjectByID(TypeID, out StructureTypeObj TypeObj) || TypeObj is null)
            {
                //TODO: Prompt the user with a dialog/UI interface to choose the type
                throw new ArgumentException($"StructureTypeID {TypeID} not found when assigning type to structure");
            }

            Polyline mosaic_polygon = Transform.TryMapShapeVolumeToSection(NewVolumeShape);
            if (mosaic_polygon is null)
            {
                Trace.WriteLine("Create1DStructureAction: drawn line crosses an unmapped region of section " + SectionNumber + "; no structure created.", "WebAnnotation");
                return;
            }

            StructureObj newStruct = new(TypeObj);

            LocationObj newLocation = new(newStruct,
                                                      SectionNumber,
                                                      LocationType.OPENCURVE)
            {
                Width = Global.DefaultClosedLineWidth
            };


            newLocation.SetShapeFromGeometryInSection(Transform, mosaic_polygon.ToSqlGeometry());
            await CommitNewStructure(TypeObj, newStruct, newLocation);
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
