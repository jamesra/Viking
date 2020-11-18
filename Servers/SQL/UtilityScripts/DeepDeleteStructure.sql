DECLARE @DeleteID bigint
Set @DeleteID = 8069

if OBJECT_ID('tempdb..#StructuresToDelete') is not null
	DROP Table #StructuresToDelete

select ID into #StructuresToDelete from (Select ID from Structure where ID = @DeleteID or ParentID = @DeleteID) as ID

delete from LocationLink
where A in 
(
Select ID from Location 
where ParentID in (Select ID From #StructuresToDelete) ) 

delete from LocationLink
where B in 
(
Select ID from Location where ParentID in (Select ID From #StructuresToDelete) ) 

delete from Location
where ParentID in (Select ID From #StructuresToDelete)

delete from StructureLink where SourceID in (Select ID From #StructuresToDelete) or TargetID in (Select ID From #StructuresToDelete)

delete from Structure
where ParentID=@DeleteID

delete from Structure
where ID=@DeleteID

if OBJECT_ID('tempdb..#StructuresToDelete') is not null
	DROP Table #StructuresToDelete