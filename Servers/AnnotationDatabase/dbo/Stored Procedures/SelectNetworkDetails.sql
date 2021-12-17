
			CREATE PROCEDURE [dbo].[SelectNetworkDetails]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				DECLARE @CellsInNetwork integer_list 
				DECLARE @ChildrenInNetwork integer_list 

				insert into @CellsInNetwork select ID from NetworkStructureIDs(@IDs, @Hops)

				select S.* from Structure S
					inner join @CellsInNetwork N ON N.ID = S.ID

				select C.* from Structure C
					inner join @CellsInNetwork N ON N.ID = C.ParentID
		
				insert into @ChildrenInNetwork 
					select ChildStruct.ID from Structure S
					inner join @CellsInNetwork N ON S.ID = N.ID
					inner join Structure ChildStruct ON ChildStruct.ParentID = N.ID

				select SL.* from StructureLink SL
					where SL.SourceID in (Select ID from @ChildrenInNetwork) OR
						  SL.TargetID in (Select ID from @ChildrenInNetwork)
			END
			
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectNetworkDetails] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectNetworkDetails] TO [AnnotationPowerUser]
    AS [dbo];

