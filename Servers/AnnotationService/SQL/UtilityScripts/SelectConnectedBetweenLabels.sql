declare @StructureID int    
set @StructureID = 476

declare @StructureLabelA NVarChar(128)    
set @StructureLabelA = 'CBb4w'

declare @StructureLabelB NVarChar(128)    
set @StructureLabelB = 'CBb3m'

IF OBJECT_ID('tempdb..#ChildStructureA') IS NOT NULL DROP TABLE #ChildStructureA
IF OBJECT_ID('tempdb..#LinkedStructuresA ') IS NOT NULL DROP TABLE #LinkedStructuresA
IF OBJECT_ID('tempdb..#LinkedCellsA ') IS NOT NULL DROP TABLE #LinkedCellsA


IF OBJECT_ID('tempdb..#ChildStructureB') IS NOT NULL DROP TABLE #ChildStructureB
IF OBJECT_ID('tempdb..#LinkedStructuresB ') IS NOT NULL DROP TABLE #LinkedStructuresB
IF OBJECT_ID('tempdb..#LinkedCellsB ') IS NOT NULL DROP TABLE #LinkedCellsB

select ID into #ChildStructureA from structure where ParentID in (Select ID from Structure where Label = @StructureLabelA) and TYPEID= 28
select * into #LinkedStructuresA from StructureLink where SourceID in (Select ID from #ChildStructureA) or TargetID in (Select ID from #CHildStructureA)
select Distinct ParentID as ID into #LinkedCellsA from Structure  where ID in (select SourceID from #LinkedStructuresA) or ID in (select TargetID from #LinkedStructuresA)


select ID into #ChildStructureB from structure where ParentID in (Select ID from Structure where Label = @StructureLabelB) and TYPEID= 28
select * into #LinkedStructuresB from StructureLink where SourceID in (Select ID from #ChildStructureB) or TargetID in (Select ID from #CHildStructureB)
select Distinct ParentID as ID into #LinkedCellsB from Structure  where ID in (select SourceID from #LinkedStructuresB) or ID in (select TargetID from #LinkedStructuresB)

select ParentID, geometry::UnionAggregate( VolumeShape ) from Location where ParentID in (
	select ID from Structure where ID in (select ID FROM #LinkedCellsA) and
								   ID in (Select ID From #LinkedCellsB) and
							       LEFT(Label, 3) != 'GAC')
	group by ParentID
