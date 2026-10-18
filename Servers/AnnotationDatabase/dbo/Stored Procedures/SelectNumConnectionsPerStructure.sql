-- SelectNumConnectionsPerStructure: for each parent structure, the number of its child structures
-- that are the source or target of a StructureLink. Returns StructureID (the parent), Label (the
-- parent's label) and NumConnections, ordered by NumConnections descending.
-- Parameters: none.
-- Callers: WCF AnnotateService.getTopConnectedStructures (num = 1) through EF6
-- ConnectomeEntities.SelectNumConnectionsPerStructure; AnnotationContextProcedures wrapper.
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

