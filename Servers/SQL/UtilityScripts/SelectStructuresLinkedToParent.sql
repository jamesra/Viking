DECLARE @StructureID int
Set @StructureID = 476

select T.*, ChildST.Name from StructureType ChildST
	inner join (
	select ChildStructs.TypeID, ChildStructs.ID, ChildStructs.ChildTypeID, ConnectionStructID, Label from StructureType ST
	inner join
		(
		Select PS.ID, Label, PS.TypeID, Children.ID as ConnectionStructID, Children.TypeID as ChildTypeID from Structure PS
		inner join
		(
			select ParentID,ID, TypeID from Structure
			 WHERE (
				ID in (
				Select TargetID from StructureLink
				 WHERE (SourceID in 
						(
						SELECT Src.ID
						FROM [Rabbit].[dbo].[Structure] Src
						WHERE ParentID = @StructureID
						) 
					   )
					  )
				OR (
				ID in (
				Select SourceID from StructureLink
				 WHERE ( 
					TargetID in 
							(SELECT Targ.ID
								FROM [Rabbit].[dbo].[Structure] Targ
								WHERE ParentID = @StructureID) 
							)
					   )
					  )
					)
				) Children
			ON PS.ID = Children.ParentID 
		) ChildStructs
		ON ST.ID = ChildStructs.TypeID
	) T
	ON T.ChildTypeID = ChildST.ID
 
 