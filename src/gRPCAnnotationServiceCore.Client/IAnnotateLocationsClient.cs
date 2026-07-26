using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gRPCAnnotationServiceCore.Client
{
    public interface IAnnotateLocationsClient
    {
        [Obsolete("Use CreateLocationAsync")]
        Location CreateLocation(Location obj, long[] LinkedIDs);

        Task<Location> CreateLocationAsync(Location obj, long[] LinkedIDs);

        [Obsolete("Use GetLocationByIDAsync")]
        Location GetLocationByID(long ID);

        Task<Location> GetLocationByIDAsync(long ID);

        [Obsolete("Use GetLocationsByIDAsync")]
        Location[] GetLocationsByID(long[] IDs);

        Task<Location[]> GetLocationsByIDAsync(long[] IDs);

        [Obsolete("Use GetLastModifiedLocationAsync")]
        Location GetLastModifiedLocation();

        Task<Location> GetLastModifiedLocationAsync();

        [Obsolete("Use GetLinkedLocationsAsync")]
        long[] GetLinkedLocations(long ID);

        Task<long[]> GetLinkedLocationsAsync(long ID);

        [Obsolete("Use GetLocationsForSectionAsync")]
        Location[] GetLocationsForSection(long section, long QueryExecutedTime);

        Task<Location[]> GetLocationsForSectionAsync(long section, long QueryExecutedTime);

        [Obsolete("Use GetLocationsForStructureAsync")]
        Location[] GetLocationsForStructure(long structureID);

        Task<Location[]> GetLocationsForStructureAsync(long structureID);

        [Obsolete("Use GetLocationChangesInMosaicRegionAsync")]
        Location[] GetLocationChangesInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, long QueryExecutedTime, long[] DeletedIDs);

        Task<Location[]> GetLocationChangesInMosaicRegionAsync(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, long QueryExecutedTime, long[] DeletedIDs);

        [Obsolete("Use GetAnnotationsInMosaicRegionAsync")]
        AnnotationSet GetAnnotationsInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, long QueryExecutedTime, long[] DeletedIDs);

        Task<AnnotationSet> GetAnnotationsInMosaicRegionAsync(long section, BoundingRectangle bbox, double MinRadius, long? ModifiedAfterThisUtcTime, long QueryExecutedTime, long[] DeletedIDs);

        [Obsolete("Use GetLocationChangesAsync")]
        Location[] GetLocationChanges(long section, long ModifiedAfterThisUtcTime, long QueryExecutedTime, long[] DeletedIDs);

        Task<Location[]> GetLocationChangesAsync(long section, long ModifiedAfterThisUtcTime, long QueryExecutedTime, long[] DeletedIDs);

        [Obsolete("Use UpdateAsync")]
        long[] Update(Location[] locations);

        Task<long[]> UpdateAsync(Location[] locations);

        [Obsolete("Use CreateLocationLinkAsync")]
        void CreateLocationLink(long SourceID, long TargetID);

        Task CreateLocationLinkAsync(long SourceID, long TargetID);

        [Obsolete("Use DeleteLocationLinkAsync")]
        void DeleteLocationLink(long SourceID, long TargetID);

        Task DeleteLocationLinkAsync(long SourceID, long TargetID);

        [Obsolete("Use GetLocationLinksForSectionAsync")]
        LocationLink[] GetLocationLinksForSection(long section, long ModifiedAfterThisTime, long QueryExecutedTime, LocationLink[] DeletedLinks);

        Task<LocationLink[]> GetLocationLinksForSectionAsync(long section, long ModifiedAfterThisTime, long QueryExecutedTime, LocationLink[] DeletedLinks);

        [Obsolete("Use GetLocationLinksForSectionInMosaicRegionAsync")]
        LocationLink[] GetLocationLinksForSectionInMosaicRegion(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, long QueryExecutedTime, LocationLink[] DeletedLinks);

        Task<LocationLink[]> GetLocationLinksForSectionInMosaicRegionAsync(long section, BoundingRectangle bbox, double MinRadius, long ModifiedAfterThisUtcTime, long QueryExecutedTime, LocationLink[] DeletedLinks);

        [Obsolete("Use GetLocationChangeLogAsync")]
        LocationHistory[] GetLocationChangeLog(long? structure_id, DateTime? begin_time, DateTime? end_time);

        Task<LocationHistory[]> GetLocationChangeLogAsync(long? structure_id, DateTime? begin_time, DateTime? end_time);

    }
}

