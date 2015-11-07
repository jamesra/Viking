IF OBJECT_ID (N'dbo.ufnLineFromPoints', N'FN') IS NOT NULL
    DROP FUNCTION ufnLineFromPoints;
GO
CREATE FUNCTION dbo.ufnLineFromPoints(@P1 geometry, @P2 geometry)
RETURNS geometry 
AS 
-- Returns a line where two circles intersect.  
-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
BEGIN
    DECLARE @ret geometry
	if @P1.Z IS NOT NULL AND @P2.Z IS NOT NULL
		SET @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1.STX, 10,8) + ' ' +
												   STR(@P1.STY, 10,8) + ' ' +
												   STR(@P1.Z, 10,8) + ', ' +
												   STR(@P2.STX, 10,8) + ' ' +
												   STR(@P2.STY, 10,8) + ' ' +
												   STR(@P2.Z, 10,8) + ')',0)
	ELSE
		SET @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1.STX, 10,8) + ' ' +
												   STR(@P1.STY, 10,8) + ', ' +
												   STR(@P2.STX, 10,8) + ' ' +
												   STR(@P2.STY, 10,8) + ')',0)
    RETURN @ret
END