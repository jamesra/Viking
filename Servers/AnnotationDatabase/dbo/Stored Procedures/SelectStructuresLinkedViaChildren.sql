
		CREATE PROCEDURE [dbo].[SelectStructuresLinkedViaChildren]
			-- Add the parameters for the stored procedure here 
			@ID bigint 
		AS
		BEGIN  
			if @ID IS NULL 
			BEGIN
				select distinct ParentID as ID from Structure
			END
			ELSE 
			BEGIN
				

				IF OBJECT_ID('tempdb..#ChildStructure') IS NOT NULL
				BEGIN
					DROP TABLE #ChildStructure
				END
				IF OBJECT_ID('tempdb..#LinkedStructures') IS NOT NULL
				BEGIN
					DROP TABLE #LinkedStructures
				END

				SET NOCOUNT ON;

				select ID into #ChildStructure from structure where ParentID = @ID
				select SourceID, TargetID into #LinkedStructures 
					from StructureLink where 
						SourceID in (Select ID from #ChildStructure) 
							or
						TargetID in (Select ID from #CHildStructure)
				 
				select distinct ParentID as ID from Structure
					where ID in (select SourceID from #LinkedStructures) or ID in (select TargetID from #LinkedStructures)
			 END

		END
			
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectStructuresLinkedViaChildren] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectStructuresLinkedViaChildren] TO [AnnotationPowerUser]
    AS [dbo];

