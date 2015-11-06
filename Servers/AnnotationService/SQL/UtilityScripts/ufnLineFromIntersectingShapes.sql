IF OBJECT_ID (N'dbo.ufnLineFromIntersectingShapes', N'FN') IS NOT NULL
    DROP FUNCTION ufnLineFromIntersectingShapes;
GO
CREATE FUNCTION dbo.ufnLineFromIntersectingShapes(@S geometry, @T geometry)
RETURNS geometry 
AS 
-- Returns the stock level for the product.
BEGIN
    DECLARE @ret geometry

	IF @S.STIntersects(@T) = 1 
	BEGIN
		DECLARE @Points geometry
		SET @Points = @S.STBoundary().STIntersection(@T.STBoundary())
		SET @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@Points.STGeometryN(1).STX) + ' ' +
												   STR(@Points.STGeometryN(1).STY) + ', ' +
												   STR(@Points.STGeometryN(2).STX) + ' ' +
												   STR(@Points.STGeometryN(2).STY) + ')',0)
	END
	ELSE
	BEGIN
		DECLARE @SCenter geometry
		DECLARE @TCenter geometry
		DECLARE @Length float
		DECLARE @Angle float
		set @SCenter = @S.STCentroid ( )
		set @TCenter = @T.STCentroid ( )
		set @Length = SQRT(@S.STArea() / PI())
		set @Angle = ATN2(@TCENTER.STX - @SCenter.STX, @TCenter.STY - @SCenter.STY)
		
		set @ret = dbo.ufnLineFromAngleAndDistance( @Angle, @Length, @SCenter)
	END
			 
    RETURN @ret
END