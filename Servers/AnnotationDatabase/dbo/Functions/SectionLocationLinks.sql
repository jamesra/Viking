
			CREATE FUNCTION SectionLocationLinks(@Z float)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from LocationLink where A in (select ID from SectionLocations(@Z)) or B in (select ID from SectionLocations(@Z))
				);
			