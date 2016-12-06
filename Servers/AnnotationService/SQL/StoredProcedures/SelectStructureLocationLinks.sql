USE [Test]
GO
/****** Object:  UserDefinedFunction [dbo].[StructureLocationLinks]    Script Date: 11/23/2016 5:27:31 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

			ALTER FUNCTION [dbo].[StructureLocationLinks](@StructureID bigint)
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
			