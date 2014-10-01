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
        /// </summary>
        /// <param name="typeID"></param>
        /// <returns></returns>
        [OperationContract]
        Structure[] GetStructuresForType(long typeID);

        /// <summary>
        /// Updates or creates a new structure type
        /// </summary>
        /// <param name="structType"></param>
        /// <returns></returns>
        [OperationContract]
        long[] UpdateStructureTypes(StructureType[] structType);
        
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
