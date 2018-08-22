declare @StructureID int    
set @StructureID = 476

IF OBJECT_ID('tempdb..#ChildStructure') IS NOT NULL DROP TABLE #ChildStructure
IF OBJECT_ID('tempdb..#LinkedStructures ') IS NOT NULL DROP TABLE #LinkedStructures 

select ID into #ChildStructure from structure where ParentID = @StructureID

select * into #LinkedStructures from StructureLink where SourceID in (Select ID from #ChildStructure) or TargetID in (Select ID from #CHildStructure)

select Distinct ParentID from Structure where ID in (select SourceID from #LinkedStructures) or ID in (select TargetID from #LinkedStructures)