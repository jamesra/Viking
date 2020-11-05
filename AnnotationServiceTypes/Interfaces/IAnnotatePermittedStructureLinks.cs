using AnnotationService.Types;
using System.ServiceModel;


namespace AnnotationService.Interfaces
{
    [ServiceContract]
    public interface IAnnotatePermittedStructureLinks
    {
        [OperationContract]
        AnnotationService.Types.PermittedStructureLink[] GetPermittedStructureLinks();

        [OperationContract]
        PermittedStructureLink CreatePermittedStructureLink(PermittedStructureLink link);

        /// <summary>
        /// Updates or creates structure links
        /// </summary>
        /// <param name="structType"></param>
        /// <returns>IDs of updated</returns.
        [OperationContract]
        void UpdatePermittedStructureLinks(PermittedStructureLink[] permittedStructureLinks);

    }
}
