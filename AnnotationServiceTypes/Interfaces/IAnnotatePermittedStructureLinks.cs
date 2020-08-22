using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ServiceModel;
using AnnotationService.Types;


namespace AnnotationServiceTypes.Interfaces
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
