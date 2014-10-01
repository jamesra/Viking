
DECLARE @SourceID bigint, @TargetID bigint
DECLARE StructureLinkCursor CURSOR FOR 
	select SourceID, TargetID from StructureLink
		order by SourceID
 
OPEN StructureLinkCursor
FETCH NEXT FROM StructureLinkCursor
INTO @SourceID, @TargetID

while @@FETCH_STATUS = 0
BEGIN 
	DECLARE @SourceParentID bigint, @TargetParentID bigint

	/*select ID from Structure where ID=@ID
	select ParentID from Structure where ID=@ParentID*/
	/*select NULL in (select ParentID from Structure where ID=@SourceID or ID=@TargetID)*/
	
	set @SourceParentID = (select ParentID from Structure where ID=@SourceID);
	set @TargetParentID = (select ParentID from Structure where ID=@TargetID);
	
	/*print(NULL in (select ParentID from Structure where ID=@SourceID or ID=@TargetID))*/
	IF @SourceParentID is NULL
	BEGIN
		print @SourceID
		delete from StructureLink where SourceID = @SourceID and TargetID = @TargetID
	END
	
	IF @TargetParentID is NULL
	BEGIN
		print @TargetID
		delete from StructureLink where SourceID = @SourceID and TargetID = @TargetID
	END
	
	
	FETCH NEXT FROM StructureLinkCursor
	INTO @SourceID, @TargetID
END

CLOSE StructureLinkCursor;
DEALLOCATE StructureLinkCursor; 