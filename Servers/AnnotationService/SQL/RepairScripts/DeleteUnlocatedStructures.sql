
IF OBJECT_ID('tempdb..#UnlocatedStructures') IS NOT NULL DROP TABLE #UnlocatedStructures

--Delete structures linked to themselves
delete from StructureLink where TargetID = SourceID
go


select ID as ID into #UnlocatedStructures FROM Structure where 
	ID not in (select distinct ParentID from Location)

select * from #UnlocatedStructures order by ID

--Delete structure links to structures with no locations
delete SL from StructureLink SL
	inner join #UnlocatedStructures US ON US.ID = SL.TargetID 
go
	
delete SL from StructureLink SL
	inner join #UnlocatedStructures US ON US.ID = SL.SourceID 
go

--Print the child structures of the structure we want to delete
select ID from Structure
	where ParentID in ((select ID from #UnlocatedStructures))

--Try to delete the structures
delete S from Structure S
	inner join #UnlocatedStructures US ON US.ID = S.ID



IF OBJECT_ID('tempdb..#UnlocatedStructures') IS NOT NULL DROP TABLE #UnlocatedStructures