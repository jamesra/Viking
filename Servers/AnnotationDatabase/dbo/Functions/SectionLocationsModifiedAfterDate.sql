-- SectionLocationsModifiedAfterDate: Location rows on section @Z with LastModified >= @QueryDate.
-- @Z: section number. @QueryDate: inclusive lower bound on Location.LastModified.
-- Callers: dbo.SectionLocationLinksModifiedAfterDate; EF wrappers in ConnectomeEntities and
-- AnnotationContext (ConnectomeDataModelCore).

			CREATE FUNCTION [dbo].[SectionLocationsModifiedAfterDate](@Z float, @QueryDate datetime)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location 
					where Z = @Z AND LastModified >= @QueryDate
				);
						