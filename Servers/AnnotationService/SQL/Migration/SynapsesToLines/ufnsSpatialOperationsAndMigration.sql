
IF OBJECT_ID (N'dbo.ufnTranslatePoint', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnTranslatePoint;
GO
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

IF OBJECT_ID (N'dbo.ufnVector', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnVector;
GO
CREATE FUNCTION dbo.ufnVector(@Angle float, @Magnitude float)
RETURNS geometry 
AS 
--Return A vector from origin 0,0 at Angle with magnitude M
BEGIN
	return geometry::Point(COS(@Angle) * @Magnitude,
						   SIN(@Angle) * @Magnitude, 0)
END
GO

IF OBJECT_ID (N'dbo.ufnTriangleArea', N'FN') IS NOT NULL
    DROP FUNCTION ufnTriangleArea;
GO
CREATE FUNCTION dbo.ufnTriangleArea(@P1 geometry, @P2 geometry, @P3 geometry)
RETURNS float 
AS 
-- Returns the stock level for the product.
BEGIN
    DECLARE @ret float
	DECLARE @S float
	DECLARE @A float
	DECLARE @B float
	DECLARE @C float
	set @A = @P1.STDistance(@P2)
	set @B = @P2.STDistance(@P3)
	set @C = @P3.STDistance(@P1)
	set @S = (@A + @B + @C) / 2.0
	set @ret = SQRT(@S * (@S - @A) * (@S - @B) * (@S - @C))
    RETURN @ret;
END
GO

IF OBJECT_ID (N'dbo.ufnAngleBetweenShapes', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnAngleBetweenShapes;
GO
CREATE FUNCTION [dbo].[ufnAngleBetweenShapes](@S geometry, @T geometry)
	RETURNS float 
	AS 
	-- Returns a line where two circles intersect.  
	-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
	BEGIN
		DECLARE @Angle float 

		DECLARE @SCenter geometry
		DECLARE @TCenter geometry
		set @SCenter = @S.STCentroid ( )
		set @TCenter = @T.STCentroid ( )
		set @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX)
		RETURN @Angle
	END
GO

IF OBJECT_ID (N'dbo.ufnLineFromPoints', N'FN') IS NOT NULL
    DROP FUNCTION ufnLineFromPoints;
GO
CREATE FUNCTION dbo.ufnLineFromPoints(@P1 geometry, @P2 geometry)
RETURNS geometry 
AS 
-- Returns a line where two circles intersect.  
-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
BEGIN
    DECLARE @ret geometry
	if @P1.Z IS NOT NULL AND @P2.Z IS NOT NULL
		SET @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1.STX, 10,8) + ' ' +
												   STR(@P1.STY, 10,8) + ' ' +
												   STR(@P1.Z, 10,8) + ', ' +
												   STR(@P2.STX, 10,8) + ' ' +
												   STR(@P2.STY, 10,8) + ' ' +
												   STR(@P2.Z, 10,8) + ')',0)
	ELSE
		SET @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1.STX, 10,8) + ' ' +
												   STR(@P1.STY, 10,8) + ', ' +
												   STR(@P2.STX, 10,8) + ' ' +
												   STR(@P2.STY, 10,8) + ')',0)
    RETURN @ret
END
GO

IF OBJECT_ID (N'dbo.ufnLineFromThreePoints', N'FN') IS NOT NULL
    DROP FUNCTION ufnLineFromThreePoints;
GO

CREATE FUNCTION dbo.ufnLineFromThreePoints(@P1 geometry, @P2 geometry, @P3 geometry)
RETURNS geometry 
AS 
-- Returns a line where two circles intersect.  
-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
BEGIN
    DECLARE @ret geometry
	if @P1.Z IS NOT NULL AND @P2.Z IS NOT NULL
		SET @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1.STX, 10,8) + ' ' +
												   STR(@P1.STY, 10,8) + ' ' +
												   STR(@P1.Z, 10,8) + ', ' +
												   STR(@P2.STX, 10,8) + ' ' +
												   STR(@P2.STY, 10,8) + ' ' +
												   STR(@P2.Z, 10,8) + ', ' +
												   STR(@P3.STX, 10,8) + ' ' +
												   STR(@P3.STY, 10,8) + ' ' +
												   STR(@P3.Z, 10,8) + ')',0)
	ELSE
		SET @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1.STX, 10,8) + ' ' +
												   STR(@P1.STY, 10,8) + ', ' +
												   STR(@P2.STX, 10,8) + ' ' +
												   STR(@P2.STY, 10,8) + ', ' + 
												   STR(@P3.STX, 10,8) + ' ' +
												   STR(@P3.STY, 10,8) + ')',0)
    RETURN @ret
END
GO

IF OBJECT_ID (N'dbo.ufnLineFromAngleAndDistance', N'FN') IS NOT NULL
    DROP FUNCTION ufnLineFromAngleAndDistance;
GO

CREATE FUNCTION dbo.ufnLineFromAngleAndDistance(@Angle float, @distance float, @offset geometry)
RETURNS geometry
AS 
-- Returns a line centered on offset with @angle and total length = @distance
BEGIN
    DECLARE @ret geometry
	DECLARE @P1X float
	DECLARE @P1Y float
	DECLARE @P2X float
	DECLARE @P2Y float
	DECLARE @Radius float
	DECLARE @Tau float
	set @Radius = @distance / 2.0 

	--Need to create a line centered on 0,0 so we can translate it to the center of S
	set @P1X = (COS(@Angle - PI()) * @Radius) + @offset.STX
	set @P1Y = (SIN(@Angle - PI()) * @Radius) + @offset.STY
	set @P2X = (COS(@Angle) * @Radius) + @offset.STX
	set @P2Y = (SIN(@Angle) * @Radius) + @offset.STY

	if @Offset.Z is NOT NULL
		set @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1X, 10,8)  + ' ' +
												STR(@P1Y, 10,8) + ' ' +
												STR(@offset.Z, 10, 8) + ', ' + 
												STR(@P2X, 10,8) + ' ' +
												STR(@P2Y, 10,8) + ' ' +
												STR(@offset.Z, 10, 8) + ')',0)
	ELSE
		set @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1X, 10,8)  + ' ' +
												STR(@P1Y, 10,8) + ', ' + 
												STR(@P2X, 10,8) + ' ' +
												STR(@P2Y, 10,8) + ')',0)
				  
    RETURN @ret
END
GO

IF OBJECT_ID (N'dbo.ufnLineFromLinkedShapes', N'FN') IS NOT NULL
    DROP FUNCTION ufnLineFromLinkedShapes;
GO
CREATE FUNCTION dbo.ufnLineFromLinkedShapes(@S geometry, @T geometry)
RETURNS geometry 
AS 
-- Returns a line where two circles intersect.  
-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
BEGIN
    DECLARE @ret geometry
	
	IF @T.STIntersects(@S) = 1 AND @S.STContains(@T) = 0 AND @T.STContains(@S) = 0
	BEGIN
		DECLARE @Points geometry
		SET @Points = @S.STBoundary().STIntersection(@T.STBoundary())
		SET @ret = DBO.ufnLineFromPoints(@Points.STGeometryN(1), @Points.STGeometryN(2))
	END
	ELSE
	BEGIN
		DECLARE @SCenter geometry
		DECLARE @TCenter geometry
		DECLARE @Radius float
		DECLARE @Angle float
		set @SCenter = @S.STCentroid ( )
		set @TCenter = @T.STCentroid ( )
		set @Radius = SQRT(@T.STArea() / PI())
		set @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX) + (PI() / 2.0)
		
		set @ret = dbo.ufnLineFromAngleAndDistance( @Angle, @Radius * 2, @TCenter)
	END
			 
    RETURN @ret
END
GO

IF OBJECT_ID (N'dbo.ufnCreateCircle', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnCreateCircle;
GO

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


IF OBJECT_ID (N'dbo.ufnLineFromThreePoints', N'FN') IS NOT NULL
    DROP FUNCTION ufnLineFromThreePoints;
GO


CREATE FUNCTION dbo.ufnLineFromThreePoints(@P1 geometry, @P2 geometry, @P3 geometry)
RETURNS geometry 
AS 
-- Returns a line where two circles intersect.  
-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
BEGIN
    DECLARE @ret geometry
	if @P1.Z IS NOT NULL AND @P2.Z IS NOT NULL
		SET @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1.STX, 10,8) + ' ' +
												   STR(@P1.STY, 10,8) + ' ' +
												   STR(@P1.Z, 10,8) + ', ' +
												   STR(@P2.STX, 10,8) + ' ' +
												   STR(@P2.STY, 10,8) + ' ' +
												   STR(@P2.Z, 10,8) + ', ' +
												   STR(@P3.STX, 10,8) + ' ' +
												   STR(@P3.STY, 10,8) + ' ' +
												   STR(@P3.Z, 10,8) + ')',0)
	ELSE
		SET @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1.STX, 10,8) + ' ' +
												   STR(@P1.STY, 10,8) + ', ' +
												   STR(@P2.STX, 10,8) + ' ' +
												   STR(@P2.STY, 10,8) + ', ' + 
												   STR(@P3.STX, 10,8) + ' ' +
												   STR(@P3.STY, 10,8) + ')',0)
    RETURN @ret
END
GO


IF OBJECT_ID (N'dbo.ufnWeightedMidpointBetweenCircles', N'FN') IS NOT NULL
    DROP FUNCTION ufnWeightedMidpointBetweenCircles;
GO
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

IF OBJECT_ID (N'dbo.ufnIntersectingCurveForCircles', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnIntersectingCurveForCircles;
GO

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

IF OBJECT_ID (N'dbo.ufnLineFromAngleAndDistance', N'FN') IS NOT NULL
    DROP FUNCTION ufnLineFromAngleAndDistance;
GO
CREATE FUNCTION dbo.ufnLineFromAngleAndDistance(@Angle float, @distance float, @offset geometry)
RETURNS geometry
AS 
-- Returns a line centered on offset with @angle and total length = @distance
BEGIN
    DECLARE @ret geometry
	DECLARE @P1X float
	DECLARE @P1Y float
	DECLARE @P2X float
	DECLARE @P2Y float
	DECLARE @Radius float
	DECLARE @Tau float
	set @Radius = @distance / 2.0 

	--Need to create a line centered on 0,0 so we can translate it to the center of S
	set @P1X = (COS(@Angle - PI()) * @Radius) + @offset.STX
	set @P1Y = (SIN(@Angle - PI()) * @Radius) + @offset.STY
	set @P2X = (COS(@Angle) * @Radius) + @offset.STX
	set @P2Y = (SIN(@Angle) * @Radius) + @offset.STY

	if @Offset.Z is NOT NULL
		set @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1X, 10,8)  + ' ' +
												STR(@P1Y, 10,8) + ' ' +
												STR(@offset.Z, 10, 8) + ', ' + 
												STR(@P2X, 10,8) + ' ' +
												STR(@P2Y, 10,8) + ' ' +
												STR(@offset.Z, 10, 8) + ')',0)
	ELSE
		set @ret = geometry::STLineFromText( 'LINESTRING ( ' + STR(@P1X, 10,8)  + ' ' +
												STR(@P1Y, 10,8) + ', ' + 
												STR(@P2X, 10,8) + ' ' +
												STR(@P2Y, 10,8) + ')',0)
				  
    RETURN @ret
END
GO

IF OBJECT_ID (N'dbo.ufnLineThroughCircle', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnLineThroughCircle;
GO
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


IF OBJECT_ID (N'dbo.ufnPerpendicularLineThroughCircle', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnPerpendicularLineThroughCircle;
GO
CREATE FUNCTION dbo.ufnPerpendicularLineThroughCircle(@S geometry, @T geometry)
RETURNS geometry 
AS 
--Return a line passing through the center of circle S perpendicular to target point @T
BEGIN
	RETURN dbo.ufnLineThroughCircle(@S,@T,1)
END
GO


IF OBJECT_ID (N'dbo.ufnParallelLineThroughCircle', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnParallelLineThroughCircle;
GO
CREATE FUNCTION dbo.ufnParallelLineThroughCircle(@S geometry, @T geometry)
RETURNS geometry 
AS 
--Return a line passing through the center of circle S towards target point @T
BEGIN
	RETURN dbo.ufnLineThroughCircle(@S,@T,0)
END
GO

IF OBJECT_ID (N'dbo.ufnPerpendicularLineToIntersectionPointOfCircles', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnPerpendicularLineToIntersectionPointOfCircles;
GO
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
		

IF OBJECT_ID (N'dbo.ufnParallelLineForLinkedShapes', N'FN') IS NOT NULL
    DROP FUNCTION dbo.ufnParallelLineForLinkedShapes;
GO

CREATE FUNCTION dbo.ufnParallelLineForLinkedShapes(@S geometry, @T geometry)
	RETURNS geometry 
AS 
-- Returns a line where two circles intersect.  
-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
BEGIN
    DECLARE @ret geometry
	
	IF @T.STIntersects(@S) = 1 AND @S.STContains(@T) = 0 AND @T.STContains(@S) = 0
	BEGIN
		set @ret = dbo.ufnIntersectingCurveForCircles(@S,@T, 8.0)
	END
	ELSE
	BEGIN
		set @ret = dbo.ufnParallelLineThroughCircle(@S, @T)
	END

    RETURN @ret
END
GO

IF OBJECT_ID (N'dbo.ufnPerpendicularLineForLinkedShapes', N'FN') IS NOT NULL
    DROP FUNCTION ufnPerpendicularLineForLinkedShapes;
GO
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