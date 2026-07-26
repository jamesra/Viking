
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
						