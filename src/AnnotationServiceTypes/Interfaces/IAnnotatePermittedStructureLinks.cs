using AnnotationService.Types;


namespace AnnotationService.Interfaces
{
    /* Recoded [ServiceContract] */
    public interface IAnnotatePermittedStructureLinks
    {
        /* Recoded [OperationContract] */
        AnnotationService.Types.PermittedStructureLink[] GetPermittedStructureLinks();

        /* Recoded [OperationContract] */
        PermittedStructureLink CreatePermittedStructureLink(PermittedStructureLink link);

        /// <summary>
        /// Updates or creates structure links
        /// </summary>
        /// <param name="structType"></param>
        /// <returns>IDs of updated</returns.
        /* Recoded [OperationContract] */
        void UpdatePermittedStructureLinks(PermittedStructureLink[] permittedStructureLinks);

    }
}

