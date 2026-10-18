-- SectionLocationLinksModifiedAfterDate: LocationLink rows where endpoint A or B is a location on
-- section @Z whose LastModified >= @QueryDate (see dbo.SectionLocationsModifiedAfterDate).
-- The filter is on the endpoint location's LastModified, not on the link.
-- @Z: section number. @QueryDate: inclusive lower bound on Location.LastModified.
-- Callers: gRPC LocationService.GetLocationLinksForSection (with a modified-after time), via
-- AnnotationContext.SectionLocationLinksModifiedAfterDate; EF6 wrapper in ConnectomeEntities.

			CREATE FUNCTION SectionLocationLinksModifiedAfterDate(@Z float, @QueryDate datetime)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from LocationLink where A in (select ID from SectionLocationsModifiedAfterDate(@Z, @QueryDate)) or B in (select ID from SectionLocationsModifiedAfterDate(@Z, @QueryDate))
				);
			