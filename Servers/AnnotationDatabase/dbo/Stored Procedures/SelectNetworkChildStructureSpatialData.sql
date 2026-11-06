-- SelectNetworkChildStructureSpatialData: StructureSpatialCache rows of the linked child structures
-- of the network within @Hops links of @IDs (dbo.NetworkChildStructureIDs).
-- Parameters: @IDs, the seed structure IDs; @Hops, the number of link hops to follow.
-- Callers: OData StructureSpatialCaches controller through EF6
-- ConnectomeEntities.SelectNetworkChildStructureSpatialData; AnnotationContextProcedures wrapper.
		    CREATE PROCEDURE [dbo].[SelectNetworkChildStructureSpatialData]
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				SET NOCOUNT ON;

				select S.* from StructureSpatialCache S 
					inner join NetworkChildStructureIDs(@IDs, @Hops) N ON N.ID = S.ID
			END