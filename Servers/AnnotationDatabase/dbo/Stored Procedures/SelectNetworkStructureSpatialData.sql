-- SelectNetworkStructureSpatialData: StructureSpatialCache rows of the network within @Hops links
-- of @IDs (dbo.NetworkStructureIDs).
-- Parameters: @IDs, the seed structure IDs; @Hops, the number of link hops to follow.
-- Callers: OData StructureSpatialCaches controller through EF6
-- ConnectomeEntities.SelectNetworkStructureSpatialData; AnnotationContextProcedures wrapper.
		    CREATE PROCEDURE [dbo].[SelectNetworkStructureSpatialData]
				@IDs integer_list READONLY,
				@Hops int
			AS
			BEGIN
				SET NOCOUNT ON;

				select S.* from StructureSpatialCache S 
					inner join NetworkStructureIDs(@IDs, @Hops) N ON N.ID = S.ID
			END