using AnnotationService.Types;
using System;
using System.ServiceModel;

namespace AnnotationService.Interfaces
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
        StructureType GetStructureTypeByID(Int64 ID);

        /// <summary>
        /// Return a single structure type in the database
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        StructureType[] GetStructureTypesByIDs(Int64[] IDs);

        /// <summary>
        /// Returns all structures with the given typeID
        /// Deprecated, moved to IAnnotateStructures.GetStructuresOfType
        /// </summary>
        /// <param name="typeID"></param>
        /// <returns></returns>
        //[OperationContract]
        //Structure[] GetStructuresForType(Int64 typeID);



        /// <summary>
        /// Updates or creates a new structure type 
        /// </summary>
        /// <param name="structType"></param>
        /// <returns></returns>
        [OperationContract]
        Int64[] UpdateStructureTypes(StructureType[] structType);

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
