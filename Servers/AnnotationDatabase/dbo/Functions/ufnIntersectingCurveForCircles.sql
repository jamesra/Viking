
				CREATE FUNCTION dbo.ufnIntersectingCurveForCircles(@S geometry, @T geometry, @SeperateDistance float)
				RETURNS geometry 
				AS 
					--Returns a three part line that passes through the points where the two circles intersect and the weighted
					--midpoint between the circles
					--@SeperateDistance Nudges the line away from the target geometry by a set distance.  Used to prevent lines from 
					--perfectly overlapping when migrating linked source and target locations
				BEGIN
					DECLARE @ret geometry
					DECLARE @Points geometry
					DECLARE @Midpoint geometry
					DECLARE @SBounds geometry
					DECLARE @TBounds geometry

					IF(@S.STDimension() > 1)
						set @SBounds = @S.STBoundary() 
					ELSE
						set @SBounds = @S

					IF(@T.STDimension() > 1)
						set @TBounds = @T.STBoundary() 
					ELSE
						set @TBounds = @T

					SET @Points = @SBounds.STIntersection(@TBounds)
					SET @Midpoint = dbo.ufnWeightedMidpointBetweenCircles(@S, @T)

					DECLARE @Startpoint geometry
					DECLARE @Endpoint geometry
					set @Startpoint = @Points.STGeometryN(1)
					set @Endpoint = @Points.STGeometryN(2)
	
					IF @SeperateDistance IS NOT NULL
					BEGIN
						DECLARE @Angle float
						DECLARE @TranslateVector geometry
						set @Angle = dbo.ufnAngleBetweenShapes(@S,@T)
						set @TranslateVector = dbo.ufnVector(@Angle, @SeperateDistance)
						set @Startpoint = dbo.ufnTranslatePoint(@Startpoint, @TranslateVector)
						set @Endpoint = dbo.ufnTranslatePoint(@Endpoint, @TranslateVector)
						set @Midpoint = dbo.ufnTranslatePoint(@Midpoint, @TranslateVector)

					END

					SET @ret = dbo.ufnLineFromThreePoints(@Startpoint, @Midpoint, @Endpoint)
					RETURN @ret
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnIntersectingCurveForCircles] TO PUBLIC
    AS [dbo];

