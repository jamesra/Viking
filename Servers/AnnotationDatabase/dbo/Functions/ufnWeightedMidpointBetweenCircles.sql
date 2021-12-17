
				CREATE FUNCTION dbo.ufnWeightedMidpointBetweenCircles(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
					--We are trying to find the point between two circles where the distances, normalized by ratio,
					-- from the centers to point P are equal.  We call this normalized value Z1 & Z2 for Circle 1 & 2
					-- Z should be from 0 to 1 for each circle.  If it is not we return NULL.  
					-- There are two possible Z values.  If both fall within 0 to 1 we take the one between the circles
				BEGIN
					DECLARE @ret geometry
					DECLARE @Distance float
					DECLARE @SCenter geometry
					DECLARE @TCenter geometry
					DECLARE @SRadius float
					DECLARE @TRadius float
					DECLARE @Angle float
					DECLARE @X1_MID float
					DECLARE @Y1_MID float

					DECLARE @X2_MID float
					DECLARE @Y2_MID float

					DECLARE @RadiusDiff float
					DECLARE @RadiusSum float
					DECLARE @RadiusRatio float

					DECLARE @Z1 float
					DECLARE @Z2 float

					DECLARE @S_MID_DIST1 float
					DECLARE @T_MID_DIST1 float
					DECLARE @S_MID_DIST2 float
					DECLARE @T_MID_DIST2 float

					set @SCenter = @S.STCentroid ( )
					set @TCenter = @T.STCentroid ( )
					set @SRadius = SQRT(@S.STArea() / PI())
					set @TRadius = SQRT(@T.STArea() / PI())

					set @RadiusDiff = @TRadius - @SRadius
					set @RadiusSum = @TRadius + @SRadius

					IF @RadiusDiff = 0 BEGIN
						return geometry::Point((@SCenter.STX + @TCenter.STX) / 2.0,
											   (@SCenter.STY + @TCenter.STY) / 2.0,
											   0)
					END

					--There are two possible midpoints
					set @X1_MID = ((-@SRadius * @TCenter.STX) / @RadiusDiff) + ((@TRadius * @SCenter.STX) / @RadiusDiff)
					set @X2_MID = ((@SRadius * @TCenter.STX) / @RadiusSum) + ((@TRadius * @SCenter.STX) / @RadiusSum)

					set @Y1_MID = ((-@SRadius * @TCenter.STY) / @RadiusDiff) + ((@TRadius * @SCenter.STY) / @RadiusDiff)
					set @Y2_MID = ((@SRadius * @TCenter.STY) / @RadiusSum) + ((@TRadius * @SCenter.STY) / @RadiusSum)


					set @S_MID_DIST1 = SQRT(POWER(@X1_MID - @SCenter.STX,2) + POWER(@Y1_MID - @SCenter.STY,2))
					set @S_MID_DIST2 = SQRT(POWER(@X2_MID - @SCenter.STX,2) + POWER(@Y2_MID - @SCenter.STY,2))

					--set @T_MID_DIST1 = SQRT(POWER(@X1_MID - @TCenter.STX,2) + POWER(@Y1_MID - @TCenter.STY,2))
					--set @T_MID_DIST2 = SQRT(POWER(@X2_MID - @TCenter.STX,2) + POWER(@Y2_MID - @TCenter.STY,2))

					set @Z1 = @S_MID_DIST1 / @SRadius
					set @Z2 = @S_MID_DIST2 / @SRadius

					IF(@Z1 > 1.0 AND @Z2 > 1.0)
						return NULL
	
					IF(@Z1 <= @Z2)
						return geometry::Point(@X1_MID, @Y1_MID, 0)
					ELSE
						return geometry::Point(@X2_MID, @Y2_MID, 0)
	
					--set @Angle = dbo.ufnAngleBetweenShapes(@S,@T)
					--set @Distance = @SCenter.STDistance(@TCenter)

					RETURN @ret
				END

				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnWeightedMidpointBetweenCircles] TO PUBLIC
    AS [dbo];

