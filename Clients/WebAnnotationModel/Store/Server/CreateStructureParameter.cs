using Viking.AnnotationServiceTypes.Interfaces;

namespace WebAnnotationModel.ServerInterface
{
    public readonly struct CreateStructureRequestParameter : ICreateStructureAndLocationRequestParameter
    {
        public readonly IStructureReadOnly Structure;
        public readonly ILocationReadOnly Location;

        IStructureReadOnly ICreateStructureAndLocationRequestParameter.Structure => Structure;

        ILocationReadOnly ICreateStructureAndLocationRequestParameter.Location => Location;

        public CreateStructureRequestParameter(IStructureReadOnly structure, ILocationReadOnly location)
        {
            Structure = structure;
            Location = location;
        }
    }

    public readonly struct CreateStructureResponseParameter : ICreateStructureResponseParameter
    {
        public readonly IStructure Structure;
        public readonly ILocation Location;

        IStructure ICreateStructureResponseParameter.Structure => Structure;

        ILocation ICreateStructureResponseParameter.Location => Location;

        public CreateStructureResponseParameter(IStructure structure, ILocation location)
        {
            Structure = structure;
            Location = location;
        }
    }
}