
DECLARE @ID bigint, @ParentID bigint
DECLARE ImproperParentCursor CURSOR FOR 
	select ID,ParentID from Structure where ParentID in 
		(
			select ID from Structure where not ParentID is NULL 
			and ID in
				(
					select ParentID from Structure where not ParentID is NULL
				)
		)
		order by ID
 
OPEN ImproperParentCursor
FETCH NEXT FROM ImproperParentCursor
INTO @ID, @ParentID

while @@FETCH_STATUS = 0
BEGIN
	PRINT @ID
	
	/*select ID from Structure where ID=@ID
	select ParentID from Structure where ID=@ParentID*/
	update Structure set ParentID = (select ParentID from Structure where ID=@ParentID) where ID=@ID
	
	FETCH NEXT FROM ImproperParentCursor
	INTO @ID, @ParentID
END

CLOSE ImproperParentCursor;
DEALLOCATE ImproperParentCursor; 