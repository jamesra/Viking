
				CREATE FUNCTION dbo.ufnLineThroughCircle(@S geometry, @T geometry, @Perpendicular bit)
				RETURNS geometry 
				AS 
				--Return a line passing through the center of circle S perpendicular to target point @T
				BEGIN
					DECLARE @SCenter geometry
					DECLARE @TCenter geometry
					DECLARE @ret geometry
					DECLARE @Radius float
					DECLARE @Angle float
					set @SCenter = @S.STCentroid ( )
					set @TCenter = @T.STCentroid ( )
					set @Radius = SQRT(@S.STArea() / PI())
					set @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX)
					IF @Perpendicular = 1
						set @Angle = @Angle + (PI() / 2.0)
		
					set @ret = dbo.ufnLineFromAngleAndDistance( @Angle, @Radius * 2, @SCenter)
					RETURN @ret
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnLineThroughCircle] TO PUBLIC
    AS [dbo];

