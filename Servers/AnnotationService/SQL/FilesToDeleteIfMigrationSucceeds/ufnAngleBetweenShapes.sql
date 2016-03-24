USE [Test]
GO
/****** Object:  UserDefinedFunction [dbo].[ufnLineFromLinkedShapes]    Script Date: 1/4/2016 3:25:38 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
	CREATE FUNCTION [dbo].[ufnAngleBetweenShapes](@S geometry, @T geometry)
	RETURNS float 
	AS 
	-- Returns a line where two circles intersect.  
	-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
	BEGIN
		DECLARE @Angle float 

		DECLARE @SCenter geometry
		DECLARE @TCenter geometry
		set @SCenter = @S.STCentroid ( )
		set @TCenter = @T.STCentroid ( )
		set @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX)
		RETURN @Angle
	END
			