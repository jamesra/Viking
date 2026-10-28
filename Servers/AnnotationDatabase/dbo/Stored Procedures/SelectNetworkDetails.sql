-- SelectNetworkDetails: three result sets for the network within @Hops links of @IDs
-- (dbo.NetworkStructureIDs): the network's structures, their child structures, and the
-- StructureLinks whose source or target is one of those children.
-- Parameters: @IDs, the seed structure IDs; @Hops, the number of link hops to follow.
-- Callers: WCF ConnectomeEntities.SelectNetworkDetails (EF6, reads the three sets with
-- NextResult); AnnotationContextProcedures.SelectNetworkDetailsAsync (EF Core, first set).
-- SET NOCOUNT ON: the table-variable inserts and deletes send no row-count messages; only the
-- result sets reach the client.
			CREATE PROCEDURE [dbo].[SelectNetworkDetails]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				SET NOCOUNT ON;

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

