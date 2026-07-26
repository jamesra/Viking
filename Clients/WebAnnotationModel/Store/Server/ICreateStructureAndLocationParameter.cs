using Viking.AnnotationServiceTypes.Interfaces;

namespace WebAnnotationModel.ServerInterface
{
    /// <summary>
    /// Information required to create a new structure
    /// </summary>
    public interface ICreateStructureAndLocationRequestParameter
    {
        IStructureReadOnly Structure { get; }
        ILocationReadOnly Location { get; }
    }

    public interface ICreateStructureResponseParameter
    {
        IStructure Structure { get; }
        ILocation Location { get; }
    }
}