-- SectionLocationLinks: LocationLink rows where endpoint A or B is a location on section @Z
-- (see dbo.SectionLocations). The other endpoint may lie on any section.
-- @Z: section number.
-- Callers: gRPC LocationService.GetLocationLinksForSection (no modified-after filter), via
-- AnnotationContext.SectionLocationLinks; EF6 wrapper ConnectomeEntities.SectionLocationLinks.

			CREATE FUNCTION SectionLocationLinks(@Z float)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from LocationLink where A in (select ID from SectionLocations(@Z)) or B in (select ID from SectionLocations(@Z))
				);
			