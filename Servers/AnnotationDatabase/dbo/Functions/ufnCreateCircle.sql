
				CREATE FUNCTION dbo.ufnCreateCircle(@C geometry, @Radius float)
				RETURNS geometry 
				AS 
				---Create a circle at point C with radius
				BEGIN
					declare @MinX float
					declare @MinY float
					declare @MaxX float
					declare @MaxY float

					set @MinX = @C.STX - @Radius
					set @MinY = @C.STY - @Radius
					set @MaxX = @C.STX + @Radius
					set @MaxY = @C.STY + @Radius

					RETURN geometry::STGeomFromText('CURVEPOLYGON( CIRCULARSTRING(   '+ STR(@MinX,16,2) + ' ' + STR(@C.STY,16,2) + ',' +
																		   + STR(@C.STX,16,2) + ' ' + STR(@MaxY,16,2) + ',' +
																		   + STR(@MaxX,16,2) + ' ' + STR(@C.STY,16,2) + ',' +
																		   + STR(@C.STX,16,2) + ' ' + STR(@MinY,16,2) + ',' +
																		   + STR(@MinX,16,2) + ' ' + STR(@C.STY,16,2) + ' ))', 0);
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnCreateCircle] TO PUBLIC
    AS [dbo];

