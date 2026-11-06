-- SelectNetworkChildStructureIDs: IDs of the linked child structures of the network within @Hops
-- links of @IDs (dbo.NetworkChildStructureIDs).
-- Parameters: @IDs, the seed structure IDs; @Hops, the number of link hops to follow.
-- Callers: AnnotationContextProcedures.SelectNetworkChildStructureIDsAsync (EF Core).
CREATE PROCEDURE [dbo].SelectNetworkChildStructureIDs
				@IDs integer_list READONLY,
				@Hops int
			AS
			BEGIN
				SET NOCOUNT ON;

				select N.ID as ID from NetworkChildStructureIDs(@IDs, @Hops) N
			END
