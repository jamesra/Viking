-- SelectNetworkStructureLinks: StructureLinks whose source or target is a linked child structure of
-- the network within @Hops links of @IDs (dbo.NetworkChildStructureIDs).
-- Parameters: @IDs, the seed structure IDs; @Hops, the number of link hops to follow.
-- Callers: gRPC StructureService through AnnotationContextProcedures.SelectNetworkStructureLinksAsync;
-- OData StructureLinks controller through EF6 ConnectomeEntities.SelectNetworkStructureLinks.
			CREATE PROCEDURE [dbo].[SelectNetworkStructureLinks]
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				SET NOCOUNT ON;

				select SL.* from StructureLink SL
					where SL.SourceID in (Select ID from NetworkChildStructureIDs( @IDs, @Hops)) OR
							SL.TargetID in (Select ID from NetworkChildStructureIDs( @IDs, @Hops))
			END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectNetworkStructureLinks] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectNetworkStructureLinks] TO [AnnotationPowerUser]
    AS [dbo];

