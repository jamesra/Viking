using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ServiceModel;
using Annotation;

namespace Annotation.Service.Interfaces
{
    
    [ServiceContract]
    public interface IAnnotateLocations
    {
        /// <summary>
        /// Create a new location in the database.  Generate Location links for the passed LinkedIDs
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="LinkedIDs"></param>
        /// <returns>Return new Location object with Database generated ID</returns>
        [OperationContract]
        Location CreateLocation(Location obj, long[] LinkedIDs);

        /// <summary>
        /// Return a single location from the Database
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        Location GetLocationByID(long ID);

        /// <summary>
        /// Return multiple locations from the Database
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        Location[] GetLocationsByID(long[] IDs);

        /// <summary>
        /// Return the last location modified by the calling user
        /// </summary>
        /// <param name="Username"></param>
        /// <returns></returns>
        [OperationContract]
        Location GetLastModifiedLocation();

        /// <summary>
        /// Return Location IDs linked to the passed location ID
        /// </summary>
        /// <param name="IDs"></param>
        /// <returns></returns>
        [OperationContract]
        long[] GetLinkedLocations(long ID);

        /// <summary>
        /// Find all locations in a given section.  Returns time query executed so GetLocationChanges can be called i
        /// the future
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        Location[] GetLocationsForSection(long section, out long QueryExecutedTime);

        /// <summary>
        /// Return all locations for this structure
        /// </summary>
        /// <returns></returns>
        [OperationContract]
        Location[] GetLocationsForStructure(long structureID);

        /// <summary>
        /// Returns all locations modified after a set date.  
        /// The passed tick count needs to be in the same timezone as the server
        /// </summary>
        /// <param name="section"></param>
        /// <param name="ModifiedAfterThisTime">A Datetime converted to a long. Clients should use server time</param>
        /// <param name="IDs"></param>
        /// <returns></returns>
        [OperationContract]
        Location[] GetLocationChanges(long section, long ModifiedAfterThisTime, out long QueryExecutedTime, out long[] DeletedIDs);
        
        /// <summary>
        /// Updates or creates a new structure
        /// </summary>
        /// <param name="structType"></param>
        /// <returns>ID's of updated locations</returns.
        [OperationContract]
        long[] Update(Location[] locations);

        /// <summary>
        /// Creates a link between two locations
        /// </summary>
        /// <param name="From"></param>
        /// <param name="To"></param>
        /// <returns></returns>
        [OperationContract]
        void CreateLocationLink(long SourceID, long TargetID);

        /// <summary>
        /// Deletes a link between two locations
        /// </summary>
        /// <param name="From"></param>
        /// <param name="To"></param>
        /// <returns></returns>
        [OperationContract]
        void DeleteLocationLink(long SourceID, long TargetID);

        /// <summary>
        /// Get all location links that intersect the section
        /// </summary>
        /// <param name="From"></param>
        /// <param name="To"></param>
        /// <returns></returns>
        [OperationContract]
        LocationLink[] LocationLinksForSection(long section, long ModifiedAfterThisTime, out long QueryExecutedTime, out LocationLink[] DeletedLinks);
    }
         
}
