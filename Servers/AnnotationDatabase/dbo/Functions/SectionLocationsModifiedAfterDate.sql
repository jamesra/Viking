
			CREATE FUNCTION [dbo].[SectionLocationsModifiedAfterDate](@Z float, @QueryDate datetime)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location 
					where Z = @Z AND LastModified >= @QueryDate
				);
						