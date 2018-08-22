DECLARE @StructureID int
Set @StructureID = 31942

select ParentID,ID from Location 
 WHERE (
	ID in (
	Select A from LocationLink
	 WHERE (A in 
		(
			SELECT L.ID
			FROM [Rabbit].[dbo].[Location] L
			WHERE L.ParentID = @StructureID) 
		)
		)
	OR (
	ID in (
	Select B from LocationLink
	 WHERE (B in 
		(SELECT L.ID
			FROM [Rabbit].[dbo].[Structure] L
			WHERE L.ParentID = @StructureID) 
			)
		)
	  )
	)
 
 