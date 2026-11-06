-- SelectNetworkStructures: Structure rows of the network within @Hops links of @IDs
-- (dbo.NetworkStructureIDs).
-- Parameters: @IDs, the seed structure IDs; @Hops, the number of link hops to follow.
-- Callers: OData Structures and StructureSpatialCaches controllers through EF6
-- ConnectomeEntities.SelectNetworkStructures; AnnotationContextProcedures.SelectNetworkStructuresAsync.
		    CREATE PROCEDURE [dbo].[SelectNetworkStructures]
				@IDs integer_list READONLY,
				@Hops int
			AS
			BEGIN
				SET NOCOUNT ON;

				select S.* from Structure S 
					inner join NetworkStructureIDs(@IDs, @Hops) N ON N.ID = S.ID
			END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectNetworkStructures] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectNetworkStructures] TO [AnnotationPowerUser]
    AS [dbo];

