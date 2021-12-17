
				CREATE FUNCTION dbo.ufnParallelLineThroughCircle(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				--Return a line passing through the center of circle S towards target point @T
				BEGIN
					RETURN dbo.ufnLineThroughCircle(@S,@T,0)
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnParallelLineThroughCircle] TO PUBLIC
    AS [dbo];

