
IF OBJECT_ID('tempdb..#UnlocatedStructures') IS NOT NULL DROP TABLE #UnlocatedStructures

--Delete structures linked to themselves
delete from StructureLink where TargetID = SourceID
go


select ID as ID into #UnlocatedStructures FROM Structure where 
	ID not in (select distinct ParentID from Location)

select S.ID, S.ParentID, S.Label, S.Notes from Structure S
	inner join #UnlocatedStructures  US ON US.ID = S.ID 
	order by ID

--Delete structure links to structures with no locations
delete SL from StructureLink SL
	inner join #UnlocatedStructures US ON US.ID = SL.TargetID 
go
	
delete SL from StructureLink SL
	inner join #UnlocatedStructures US ON US.ID = SL.SourceID 
go

--Print the child structures of the structure we want to delete
select ID, ParentID, Label, Notes from Structure
	where ParentID in ((select ID from #UnlocatedStructures))

DECLARE db_cursor CURSOR FOR 
	SELECT ID from #UnlocatedStructures

Declare @DeleteID bigint
OPEN db_cursor
FETCH NEXT from db_cursor INTO @DeleteID

--Try to delete the structures
while @@FETCH_STATUS = 0
BEGIN
   EXEC DeepDeleteStructure @DeleteID
   FETCH NEXT from db_cursor INTO @DeleteID
END

CLOSE db_cursor
DEALLOCATE db_cursor

IF OBJECT_ID('tempdb..#UnlocatedStructures') IS NOT NULL DROP TABLE #UnlocatedStructures