
				CREATE FUNCTION dbo.ufnVector(@Angle float, @Magnitude float)
				RETURNS geometry 
				AS 
				--Return A vector from origin 0,0 at Angle with magnitude M
				BEGIN
					return geometry::Point(COS(@Angle) * @Magnitude,
										   SIN(@Angle) * @Magnitude, 0)
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnVector] TO PUBLIC
    AS [dbo];

