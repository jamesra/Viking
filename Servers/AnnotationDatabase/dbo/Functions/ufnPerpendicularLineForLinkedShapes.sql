
				CREATE FUNCTION dbo.ufnPerpendicularLineForLinkedShapes(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				-- Returns a line where two circles intersect.  
				-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
				BEGIN
					DECLARE @ret geometry
	
					IF @T.STIntersects(@S) = 1 AND @S.STContains(@T) = 0 AND @T.STContains(@S) = 0
					BEGIN
						set @ret = dbo.ufnPerpendicularLineToIntersectionPointOfCircles(@S,@T)
					END
					ELSE
					BEGIN
						set @ret = dbo.ufnPerpendicularLineThroughCircle(@S,@T)
					END
			 
					RETURN @ret
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnPerpendicularLineForLinkedShapes] TO PUBLIC
    AS [dbo];

