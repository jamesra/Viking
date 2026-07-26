
				CREATE FUNCTION dbo.ufnPerpendicularLineThroughCircle(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				--Return a line passing through the center of circle S perpendicular to target point @T
				BEGIN
					RETURN dbo.ufnLineThroughCircle(@S,@T,1)
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnPerpendicularLineThroughCircle] TO PUBLIC
    AS [dbo];

