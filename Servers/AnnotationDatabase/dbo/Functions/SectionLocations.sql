
			CREATE FUNCTION [dbo].[SectionLocations](@Z float)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location where Z = @Z
				);
						