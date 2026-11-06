-- SelectNetworkStructureIDs: IDs of the structures within @Hops links of @IDs (dbo.NetworkStructureIDs).
-- Parameters: @IDs, the seed structure IDs; @Hops, the number of link hops to follow.
-- Callers: dbo.SelectNetworkDetails (INSERT ... EXEC); gRPC StructureService through
-- AnnotationContextProcedures.SelectNetworkStructureIDsAsync; EF6 ConnectomeEntities.SelectNetworkStructureIDs.
CREATE PROCEDURE [dbo].SelectNetworkStructureIDs
				@IDs integer_list READONLY,
				@Hops int
			AS
			BEGIN
				SET NOCOUNT ON;

				select N.ID as ID from NetworkStructureIDs(@IDs, @Hops) N
			END
