using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ServiceModel;
using Annotation;
using AnnotationService.Types;

namespace AnnotationService.Interfaces
{
    
    [ServiceContract]
    public interface IAnnotateStructures
    {
        /// <summary>
        /// Create a new structure in the database.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="LinkedIDs"></param>
        /// <returns>Return new Structure object with Database generated ID</returns>
        [OperationContract]
        CreateStructureRetval CreateStructure(Structure structure, Location location);
         
        /// <summary>
        /// Return all structures from the Database
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        Structure[] GetStructures();

        /// <summary>
        /// Return all structures from the Database for a section
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        Structure[] GetStructuresForSection(long Section, long ModifiedAfterThisTime, out long QueryExecutedTime, out long[] DeletedIDs);

        /// <summary>
        /// Returns all locations modified after a set date within the requested region
        /// The passed tick count needs to be in the same timezone as the server
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ModifiedAfterThisTime">A Datetime converted to a long. Clients should use server time</param>
        /// <param name="IDs"></param>
        /// <returns></returns>
        [OperationContract]
        Structure[] GetStructuresForSectionInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisTime, out long QueryExecutedTime, out long[] DeletedIDs);

        /// <summary>
        /// Returns all locations modified after a set date within the requested region
        /// The passed tick count needs to be in the same timezone as the server
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ModifiedAfterThisTime">A Datetime converted to a long. Clients should use server time</param>
        /// <param name="IDs"></param>
        /// <returns></returns>
        [OperationContract]
        Structure[] GetStructuresForSectionInVolumeRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisTime, out long QueryExecutedTime, out long[] DeletedIDs);
        
        /// <summary>
        /// Return a single structure from the Database
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        Structure GetStructureByID(long ID, bool IncludeChildren);

        /// <summary>
        /// Returns all linked structures
        /// </summary>
        /// <param name="ID">Array of IDs</param>
        /// <param name="IncludeChildren">Set to false if you do not want a list of child structures included for size reasons</param>
        /// <returns></returns>
        [OperationContract]
        Structure[] GetStructuresByIDs(long[] ID, bool IncludeChildren);

        /// <summary>
        /// Creates a link between two strcutures
        /// </summary>
        /// <param name="From"></param>
        /// <param name="To"></param>
        /// <returns></returns>
        [OperationContract]
        StructureLink CreateStructureLink(StructureLink link);

        /// <summary>
        /// Returns all linked structures
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        StructureLink[] GetLinkedStructures();

        /// <summary>
        /// Returns all linked structures
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        StructureLink[] GetLinkedStructuresByID(long ID);

        [OperationContract]
        long[] GetNetworkedStructures(long[] IDs, int numHops);

        [OperationContract]
        Structure[] GetChildStructuresInNetwork(long[] IDs, int numHops);

        [OperationContract]
        StructureLink[] GetStructureLinksInNetwork(long[] IDs, int numHops);

        /// <summary>
        /// Return all locations for this structure
        /// </summary>
        /// <returns></returns>
        //[OperationContract]
        //Location[] GetLocationsForStructure(long structureID);

        /// <summary>
        /// Returnst the number of locations a structure has
        /// </summary>
        /// <param name="structureID"></param>
        /// <returns></returns>
        [OperationContract]
        long NumberOfLocationsForStructure(long structureID); 

        

        /// <summary>
        /// Updates or creates a new structure 
        /// </summary>
        /// <param name="structType"></param>
        /// <returns>IDs of updated</returns.
        [OperationContract]
        long[] UpdateStructures(Structure[] structure);
         

        /// <summary>
        /// Updates or creates structure links
        /// </summary>
        /// <param name="structType"></param>
        /// <returns>IDs of updated</returns.
        [OperationContract]
        void UpdateStructureLinks(StructureLink[] structureLinks);

        /// <summary>
        /// Returns location IDs of all unfinished branches, which are locations with fewer than two location links
        /// </summary>
        /// <param name="structureID"></param>
        /// <returns></returns>
        [OperationContract]
        long[] GetUnfinishedLocations(long structureID);


        /// <summary>
        /// Returns location IDs of all unfinished branches, which are locations with fewer than two location links
        /// </summary>
        /// <param name="structureID"></param>
        /// <returns></returns>
        [OperationContract]
        LocationPositionOnly[] GetUnfinishedLocationsWithPosition(long structureID);

        /// <summary>
        /// Returns all structures with the given typeID 
        /// </summary>
        /// <param name="typeID"></param>
        /// <returns></returns>
        [OperationContract]
        Structure[] GetStructuresOfType(long typeID);

        /// <summary>
        /// Merges the specified structures into a single structure. Structures must be of the same type.
        /// </summary>
        /// <param name="KeepID"></param>
        /// <param name="MergeID"></param>
        /// <returns>ID of new structure</returns>
        [OperationContract]
        long Merge(long KeepID, long MergeID);

        /// <summary>
        /// Split the specified structure into two new structures at the specified link
        /// return an exception if the structure has a cycle in the graph.
        /// Child objects are assigned to the nearest location on the same section
        /// </summary>
        /// <param name="StructureA">Structure to split</param>
        /// <param name="locLink">Location Link to split structure at</param>
        /// <returns>ID of new structure</returns>
        [OperationContract]
        long Split(long StructureA, long LocationIDInSplitStructure);


        /// <summary>
        /// Return a list of location objects that have changed in the time interval
        /// </summary>
        /// <param name="structure_id">Optional ParentID</param>
        /// <param name="begin_time">Optional begin time</param>
        /// <param name="end_time">Optional end time</param>
        /// <returns></returns>
        [OperationContract]
        Structure[] GetStructureChangeLog(long? structure_id, DateTime? begin_time, DateTime? end_time);
    }
     
}
