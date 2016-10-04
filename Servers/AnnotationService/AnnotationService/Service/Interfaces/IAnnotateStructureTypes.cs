using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Annotation.Service.Interfaces
{
    [ServiceContract]
    public interface IAnnotateStructureTypes
    {
        /// <summary>
        /// Create a new StructureType in the database.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="LinkedIDs"></param>
        /// <returns>Return new StructureType object with database generated ID</returns>
        [OperationContract]
        StructureType CreateStructureType(StructureType obj);

        /// <summary>
        /// Return all structure types in the database
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        StructureType[] GetStructureTypes();

        /// <summary>
        /// Return a single structure type in the database
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        StructureType GetStructureTypeByID(long ID);

        /// <summary>
        /// Return a single structure type in the database
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        StructureType[] GetStructureTypesByIDs(long[] IDs);

        /// <summary>
        /// Returns all structures with the given typeID
        /// Deprecated, moved to IAnnotateStructures.GetStructuresOfType
        /// </summary>
        /// <param name="typeID"></param>
        /// <returns></returns>
        //[OperationContract]
        //Structure[] GetStructuresForType(long typeID);

        /// <summary>
        /// Updates or creates a new structure type 
        /// </summary>
        /// <param name="structType"></param>
        /// <returns></returns>
        [OperationContract]
        long[] UpdateStructureTypes(StructureType[] structType);


        [OperationContract]
        PermittedStructureLink CreatePermittedStructureLink(PermittedStructureLink link);

        /// <summary>
        /// Updates or creates structure links
        /// </summary>
        /// <param name="structType"></param>
        /// <returns>IDs of updated</returns.
        [OperationContract]
        void UpdatePermittedStructureLinks(PermittedStructureLink[] permittedStructureLinks);

        /// <summary>
        /// A test method used to ensure the service can handle a basic call
        /// </summary>
        /// <param name="structType"></param>
        /// <returns></returns>
        [OperationContract]
        string TestMethod();


        /// <summary>
        /// Return all structure types in the database
        /// </summary>
        /// <returns></returns>
//        [OperationContract]
//        StructureType[] GetStructureTemplates();

        /// <summary>
        /// Return all structure types in the database
        /// </summary>
        /// <returns></returns>
//        [OperationContract]
//        StructureType[] UpdateStructureTemplates();
    }
}
