-- SelectStructureCenter3D: the volume-space centre (X, Y, Z) of each given structure, the mean
-- of its locations' VolumeX, VolumeY and Z weighted by each location's share of the structure's
-- total MosaicShape length (lines) or area (polygons); points weigh 0. A structure whose total
-- is 0 gets X = Y = Z = 0.
-- Parameters: @IDs, the structure IDs.
-- Callers: none in the services; Servers/SQL/InDevelopment scripts (INSERT ... EXECUTE).
-- SET NOCOUNT ON: the table-variable inserts and deletes send no row-count messages; only the
-- result sets reach the client.
CREATE PROCEDURE SelectStructureCenter3D(
	@IDs integer_list READONLY
	)
AS
BEGIN
	SET NOCOUNT ON;

	declare @TotalArea TABLE(
		StructureID bigint NOT NULL PRIMARY KEY,
		TotalArea float NOT NULL
	);
	  
	DECLARE @StructureLocations TABLE(
		StructureID bigint NOT NULL,
		Area Float NOT NULL,
		X Float NOT NULL,
		Y Float NOT NULL,
		Z Float NOT NULL
	);

	DECLARE @StructureLocationWeights TABLE(
		StructureID bigint NOT NULL,
		[Weight] Float NOT NULL,
		X Float NOT NULL,
		Y Float NOT NULL,
		Z Float NOT NULL
	);
	  
	insert into @StructureLocations 
		select L.ParentID as StructureID, 
			case L.VolumeShape.STDimension()
				when 0 then 0
				when 1 then L.MosaicShape.STLength()
				when 2 then L.MosaicShape.STArea()
				else 0
			end as Area,
			L.VolumeX as X,
			L.VolumeY as Y,
			L.Z as Z 
		from Location L 
		inner join @IDs id_set on id_set.ID = L.ParentID

	insert into @TotalArea 
		Select L.StructureID as StructureID, SUM(L.Area) as TotalArea 
		from @StructureLocations L
		group by L.StructureID

	
	insert into @StructureLocationWeights
		select SL.StructureID, 
			CASE 
				WHEN A.TotalArea > 0 THEN SL.Area / A.TotalArea
				ELSE 0
			END as [Weight],
			SL.X, SL.Y, SL.Z
			from @StructureLocations SL
			inner join @TotalArea A ON A.StructureID = SL.StructureID

	select SLW.StructureID as StructureID, SUM(SLW.X * SLW.[Weight]) as X,  SUM(SLW.Y * SLW.[Weight]) as Y,  SUM(SLW.Z * SLW.[Weight]) as Z 
		from @StructureLocationWeights SLW
		group by SLW.StructureID 
END
