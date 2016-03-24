IF OBJECT_ID ('dbo.vNearestParentStructureLocation', 'view') IS NOT NULL
	DROP VIEW vNearestParentStructureLocation
GO

IF OBJECT_ID ('dbo.vParentStructureLocationDistance', 'view') IS NOT NULL
	DROP VIEW vParentStructureLocationDistance
GO

DROP STATISTICS [Location].[_dta_stat_Location_ParentID_ID_Z], [Location].[_dta_stat_Location_Z_ID]
DROP INDEX Z on dbo.Location

ALTER TABLE dbo.Location ALTER COLUMN Z int 
GO

CREATE NONCLUSTERED INDEX [Z] ON [dbo].[Location] 
	(
		[Z] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
GO

CREATE STATISTICS [_dta_stat_Location_ParentID_ID_Z] ON [dbo].[Location]([ParentID], [ID], [Z])
CREATE STATISTICS [_dta_stat_Location_Z_ID] ON [dbo].[Location]([Z], [ID])
GO

CREATE VIEW vParentStructureLocationDistance WITH SCHEMABINDING
AS
	--Create all combinations of child structure locations and parent structure locations on a section
	select L.ID as ChildLocationID, MIN(L.MosaicShape.STDistance(PL.MosaicShape)) as MinDistance
		from dbo.Location L
		INNER JOIN dbo.Structure S ON S.ID = L.ParentID
		INNER JOIN dbo.Structure PS ON S.ParentID = PS.ID
		INNER JOIN dbo.Location PL ON PL.ParentID = PS.ID AND PL.Z = L.Z
		GROUP BY L.ID
GO 

CREATE VIEW vNearestParentStructureLocation WITH SCHEMABINDING
AS
	select L.ParentID as ChildStructureID, L.ID as ChildLocationID,  L.Z as Z,
	   PL.ParentID as ParentStructureID, PL.ID as ParentLocationID
	from dbo.Location L
	
	INNER JOIN dbo.Structure S ON S.ID = L.ParentID
	INNER JOIN dbo.Structure PS ON S.ParentID = PS.ID
	INNER JOIN dbo.Location PL ON PL.ParentID = PS.ID AND PL.Z = L.Z
	INNER JOIN dbo.vParentStructureLocationDistance D ON D.ChildLocationID = L.ID --Moving the subquery to a view took a 5 minute query to 3 minutes
	WHERE L.MosaicShape.STDistance(PL.MosaicShape) = D.MinDistance 
GO

--------------------------------------

IF OBJECT_ID('vUnlinkedLocations', 'view') IS NOT NULL DROP VIEW vUnlinkedLocations
GO
IF OBJECT_ID('vChildStructureLocations', 'view') IS NOT NULL DROP VIEW vChildStructureLocations
GO

CREATE VIEW vChildStructureLocations WITH SCHEMABINDING
AS 
	select L.ID as ID, L.ParentID as StructureID, L.MosaicShape as MosaicShape, S.ParentID as StructuresParentID, S.TypeID as TypeID
	from dbo.Location L
		JOIN dbo.Structure S on S.ID = L.ParentID
		where S.ParentID IS NOT NULL
GO

CREATE UNIQUE CLUSTERED INDEX IDX_vChildStructureLocations_ChildLocationID 
	ON vChildStructureLocations (ID)
GO

CREATE VIEW vUnlinkedLocations WITH SCHEMABINDING
AS 
	select VCS.ID as ID, VCS.StructureID as StructureID, VCS.MosaicShape as MosaicShape, VCS.StructuresParentID as StructuresParentID, VCS.TypeID as TypeID
	from dbo.vChildStructureLocations VCS
	WHERE VCS.StructureID not in ((select SourceID from dbo.StructureLink) UNION (Select TargetID from dbo.StructureLink))
GO

----------------------------------

IF OBJECT_ID('StructureLinkLocations', 'view') IS NOT NULL DROP VIEW StructureLinkLocations
GO

CREATE VIEW StructureLinkLocations
as 
	select S.ID as SID, S.ParentID as SParentID, S.MosaicShape as SMosaicShape, S.Z as SZ, SourceStruct.TypeID as SourceTypeID,
	   T.ID as TID, T.ParentID as TParentID, T.MosaicShape as TMosaicShape, T.Z as TZ, TargetStruct.TypeID as TargetTypeID
	from Location S 
		join StructureLink L ON L.SourceID = S.ParentID
		join Location T      ON L.TargetID = T.ParentID AND T.Z = S.Z
		join Structure SourceStruct ON S.ParentID = SourceStruct.ID
		join Structure TargetStruct ON T.ParentID = TargetStruct.ID 

----------------------------------
GO

IF OBJECT_ID('ufnAngleBetweenShapes') IS NOT NULL DROP FUNCTION dbo.ufnAngleBetweenShapes
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


----------------------------------------
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
	
	RETURN @ret
END

