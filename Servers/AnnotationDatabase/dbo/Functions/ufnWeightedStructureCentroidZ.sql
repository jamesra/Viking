-- ufnWeightedStructureCentroidZ: Z centroid of structure @StructureID's locations, each location weighted by its
-- share of the structure's total MosaicShape area (polygons) or length (lines); points weigh 0.
-- Returns NULL when the structure has no locations, and 0 when every weight is 0.
-- @StructureID: Structure.ID whose locations (Location.ParentID) are averaged.
-- Callers: Servers/SQL/UtilityScripts/SelectChildStructureCentroids.sql; no service calls it.

	CREATE FUNCTION [dbo].[ufnWeightedStructureCentroidZ]
	(
		@StructureID bigint
	)
	RETURNS float /*
	The center of the structure annotations in Z, weighted by either area of each location or the length of the location.
	This function cannot return the XY position because it is a scalar valued function.  If you need
	XYZ, use the WeightedStructureCenters3D Table Valued Function instead. */
	AS
	BEGIN 
    
	DECLARE @StructureLocations TABLE(
		StructureID bigint NOT NULL,
		Area float NOT NULL, 
		Z float NOT NULL
	);

	DECLARE @StructureLocationWeights TABLE(
		StructureID bigint NOT NULL,
		[Weight] float NOT NULL, 
		Z float NOT NULL
	);

	INSERT INTO @StructureLocations 
	SELECT L.ParentID as StructureID, 
		CASE L.VolumeShape.STDimension()
			WHEN 0 THEN 0
			WHEN 1 THEN L.MosaicShape.STLength()
			WHEN 2 THEN L.MosaicShape.STArea()
			ELSE 0
		END as Area, 
		L.Z as Z 
	FROM Location L 
	WHERE L.ParentID = @StructureID
	;

	DECLARE @TotalArea FLOAT
	SELECT @TotalArea = SUM(L.Area) 
					 FROM @StructureLocations L
					 GROUP BY L.StructureID;

	INSERT INTO @StructureLocationWeights
	SELECT SL.StructureID, 
		CASE 
			WHEN @TotalArea > 0 THEN SL.Area / @TotalArea
			ELSE 0
		END as [Weight], 
		SL.Z
	FROM @StructureLocations SL 

	DECLARE @Result float 
	SELECT @Result = SUM(SLW.Z * SLW.[Weight])  
	FROM @StructureLocationWeights SLW
	GROUP BY SLW.StructureID;
	 
	-- Return the result of the function
	RETURN @Result

	END
