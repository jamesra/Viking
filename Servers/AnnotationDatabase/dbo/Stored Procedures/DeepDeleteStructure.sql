
		CREATE PROCEDURE DeepDeleteStructure
		-- Add the parameters for the stored procedure here
		@DeleteID bigint
		AS
		BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;
	
		if OBJECT_ID('tempdb..#StructuresToDelete') is not null
		BEGIN
			DROP Table #StructuresToDelete
		END

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
		BEGIN
			DROP Table #StructuresToDelete
		END
	END
	 