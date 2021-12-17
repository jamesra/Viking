

				CREATE FUNCTION dbo.ufnParallelLineForLinkedShapes(@S geometry, @T geometry)
					RETURNS geometry 
				AS 
				-- Returns a line where two circles intersect.  
				-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
				BEGIN
					DECLARE @ret geometry
	
					IF @T.STIntersects(@S) = 1 AND @S.STContains(@T) = 0 AND @T.STContains(@S) = 0
					BEGIN
						set @ret = dbo.ufnIntersectingCurveForCircles(@S,@T, 8.0)
					END
					ELSE
					BEGIN
						set @ret = dbo.ufnParallelLineThroughCircle(@S, @T)
					END

					RETURN @ret
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnParallelLineForLinkedShapes] TO PUBLIC
    AS [dbo];

