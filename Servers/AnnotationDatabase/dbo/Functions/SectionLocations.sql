-- SectionLocations: every Location row on section @Z.
-- @Z: section number (compared exactly with Location.Z).
-- Callers: dbo.SectionLocationLinks; EF6 ConnectomeEntities.ReadSectionLocations and the
-- AnnotationContext.SectionLocations wrapper (ConnectomeDataModelCore).

			CREATE FUNCTION [dbo].[SectionLocations](@Z float)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location where Z = @Z
				);
						