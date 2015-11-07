IF OBJECT_ID (N'dbo.ufnLineFromLinkedShapes', N'FN') IS NOT NULL
    DROP FUNCTION ufnLineFromLinkedShapes;
GO
CREATE FUNCTION dbo.ufnLineFromLinkedShapes(@S geometry, @T geometry)
RETURNS geometry 
AS 
-- Returns a line where two circles intersect.  
-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
BEGIN
    DECLARE @ret geometry
	
	IF @T.STIntersects(@S) = 1 AND @S.STContains(@T) = 0 AND @T.STContains(@S) = 0
	BEGIN
		DECLARE @Points geometry
		SET @Points = @S.STBoundary().STIntersection(@T.STBoundary())
		SET @ret = DBO.ufnLineFromPoints(@Points.STGeometryN(1), @Points.STGeometryN(2))
	END
	ELSE
	BEGIN
		DECLARE @SCenter geometry
		DECLARE @TCenter geometry
		DECLARE @Radius float
		DECLARE @Angle float
		set @SCenter = @S.STCentroid ( )
		set @TCenter = @T.STCentroid ( )
		set @Radius = SQRT(@T.STArea() / PI())
		set @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX) + (PI() / 2.0)
		
		set @ret = dbo.ufnLineFromAngleAndDistance( @Angle, @Radius * 2, @TCenter)
	END
			 
    RETURN @ret
END