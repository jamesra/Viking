
				CREATE FUNCTION dbo.ufnTranslatePoint(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				--Return POINT S translated by values in Point T
				BEGIN
					DECLARE @XNudge float
					DECLARE @YNudge float
	
					return geometry::Point(@S.STX + @T.STX, @S.STY + @T.STY, @S.STSrid)
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnTranslatePoint] TO PUBLIC
    AS [dbo];

