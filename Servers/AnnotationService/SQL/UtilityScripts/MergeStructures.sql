

DECLARE @KeepStructureID bigint
DECLARE @MergeStructureID bigint
Set @KeepStructureID = 179
Set @MergeStructureID = 25300

update Location 
set ParentID = @KeepStructureID 
where ParentID = @MergeStructureID

update Structure
set ParentID = @KeepStructureID 
where ParentID = @MergeStructureID

update StructureLink 
set SourceID = @KeepStructureID
where SourceID = @MergeStructureID

update StructureLink 
set TargetID = @KeepStructureID
where TargetID = @MergeStructureID

Delete Structure
where ID = @MergeStructureID



