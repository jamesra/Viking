

DECLARE @KeepStructureID bigint
DECLARE @MergeStructureID bigint
Set @KeepStructureID = 1232
Set @MergeStructureID = 2656
 
update Location 
set ParentID = @KeepStructureID 
where ParentID = @MergeStructureID

update Structure
set ParentID = @KeepStructureID 
where ParentID = @MergeStructureID

delete StructureLink where SourceID = @KeepStructureID AND TargetID = @MergeStructureID
delete StructureLink where TargetID = @KeepStructureID AND SourceID = @MergeStructureID

update StructureLink 
set SourceID = @KeepStructureID
where SourceID = @MergeStructureID

update StructureLink 
set TargetID = @KeepStructureID
where TargetID = @MergeStructureID

Delete Structure
where ID = @MergeStructureID





