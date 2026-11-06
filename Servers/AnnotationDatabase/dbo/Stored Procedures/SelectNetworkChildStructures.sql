-- SelectNetworkChildStructures: Structure rows of the linked child structures of the network within
-- @Hops links of @IDs (dbo.NetworkChildStructureIDs).
-- Parameters: @IDs, the seed structure IDs; @Hops, the number of link hops to follow.
-- Callers: gRPC StructureService through AnnotationContextProcedures.SelectNetworkChildStructuresAsync;
-- OData Structures controller through EF6 ConnectomeEntities.SelectNetworkChildStructures.
		    CREATE PROCEDURE [dbo].[SelectNetworkChildStructures]
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				SET NOCOUNT ON;

				select S.* from Structure S 
					inner join NetworkChildStructureIDs(@IDs, @Hops) N ON N.ID = S.ID
			END