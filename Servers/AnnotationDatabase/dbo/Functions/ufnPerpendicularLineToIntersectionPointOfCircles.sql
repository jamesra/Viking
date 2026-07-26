
				CREATE FUNCTION dbo.ufnPerpendicularLineToIntersectionPointOfCircles(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				-- Return a line that passes from the edge of circle S, through the center of S, and terminates at the midpoint between S AND T
				BEGIN
					DECLARE @ret geometry
					DECLARE @SCenter geometry
					DECLARE @TCenter geometry
					DECLARE @Midpoint geometry
					DECLARE @Edgepoint geometry
					DECLARE @Angle float
					DECLARE @SRadius float	
					SET @SCenter = @S.STCentroid()
					SET @TCenter = @T.STCentroid()
					SET @Midpoint = dbo.ufnWeightedMidpointBetweenCircles(@S, @T)
					SET @SRadius = SQRT(@S.STArea() / PI())

					if @SCenter.STX = @Midpoint.STX AND @SCenter.STY = @Midpoint.STY 
						return @SCenter

					SET @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX)

					--If Midpoint is NULL it means the circles do not overlap
					IF (@Midpoint IS NULL)
						return dbo.ufnLineFromAngleAndDistance(@Angle, @SRadius * 2, @SCenter)
	 
					SET @Edgepoint = dbo.ufnTranslatePoint(dbo.ufnVector(@Angle, @SRadius), @SCenter);
					SET @ret = dbo.ufnLineFromPoints(@Edgepoint, @Midpoint);
					return @ret
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnPerpendicularLineToIntersectionPointOfCircles] TO PUBLIC
    AS [dbo];

