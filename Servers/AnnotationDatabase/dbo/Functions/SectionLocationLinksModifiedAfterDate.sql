
			CREATE FUNCTION SectionLocationLinksModifiedAfterDate(@Z float, @QueryDate datetime)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from LocationLink where A in (select ID from SectionLocationsModifiedAfterDate(@Z, @QueryDate)) or B in (select ID from SectionLocationsModifiedAfterDate(@Z, @QueryDate))
				);
			