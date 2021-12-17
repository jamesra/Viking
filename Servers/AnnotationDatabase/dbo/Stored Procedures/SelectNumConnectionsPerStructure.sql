CREATE PROCEDURE [dbo].[SelectNumConnectionsPerStructure]
			AS
			 (
 select CS.ParentID as StructureID, ParentStructure.Label as Label, COUNT(CS.ParentID) as NumConnections 
 from Structure CS
	INNER JOIN Structure ParentStructure
	ON CS.ParentID = ParentStructure.ID
 WHERE (
	CS.ID in (
	Select SourceID from StructureLink
	 WHERE (SourceID in 
		(
			SELECT S.ID
			FROM [dbo].[Structure] S) 
		)
		)
	OR (
	CS.ID in (
	Select TargetID from StructureLink
	 WHERE (TargetID in 
		(SELECT S.ID
			FROM [dbo].[Structure] S) 
			)
		)
	  )
	)
group by CS.ParentID, ParentStructure.Label

)
order by NumConnections DESC
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectNumConnectionsPerStructure] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectNumConnectionsPerStructure] TO [AnnotationPowerUser]
    AS [dbo];

