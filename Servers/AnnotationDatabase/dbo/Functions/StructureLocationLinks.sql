-- StructureLocationLinks: distinct LocationLink rows with endpoint A or B on a location of
-- structure @StructureID (UNION removes duplicates when both endpoints belong to it).
-- @StructureID: Structure.ID whose locations (Location.ParentID) are matched.
-- Callers: dbo.SplitStructure; the OData StructureLocationLinks function (ConnectomeODataV4
-- StructuresController, StructureSpatialCachesController, WebApiConfig) through EF6
-- ConnectomeEntities.StructureLocationLinks; AnnotationContext.StructureLocationLinks wrapper.

			CREATE FUNCTION [dbo].[StructureLocationLinks](@StructureID bigint)
				RETURNS TABLE 
				AS
				RETURN(
 						select LLA.* from  LocationLink LLA 
						inner join Location L ON LLA.A = L.ID
						where L.ParentID = @StructureID
						union
						select LLB.* from LocationLink LLB  
						inner join Location L ON LLB.B = L.ID
						where L.ParentID = @StructureID
						)
						