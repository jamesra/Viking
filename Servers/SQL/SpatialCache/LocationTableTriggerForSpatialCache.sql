ALTER TRIGGER UpdateStructureSpatialCache
  ON Location
  AFTER INSERT, UPDATE, DELETE
as
BEGIN
	IF TRIGGER_NESTLEVEL() > 1/*this update is coming from some other trigger*/
		return

	SET NOCOUNT ON

	DELETE StructureSpatialCache 
	WHERE StructureSpatialCache.ID IN (SELECT ParentID FROM DELETED Group By ParentID)

	DELETE StructureSpatialCache 
	WHERE StructureSpatialCache.ID IN (SELECT ParentID FROM INSERTED Group By ParentID)

	INSERT INTO StructureSpatialCache
	SELECT        S.ID as ID,  
				  L.BoundingRect as BoundingRect,
				  dbo.ufnStructureArea(S.ID) as Area, 
				  dbo.ufnStructureVolume(S.ID) as Volume, 
				  L.MaxDim as MaxDimension,
				  L.MinZ as MinZ, 
				  L.MaxZ as MaxZ,
				  L.ConvexHull as ConvexHull,
				  dbo.ufnLastStructureMorphologyModification(S.ID) as LastModified

	FROM Structure S
	INNER JOIN 
		(select L.ParentID, 
		   --Geometry::UnionAggregate(L.VolumeShape) as AggregateShape,
		   Geometry::ConvexHullAggregate(L.VolumeShape) as ConvexHull,
		   Geometry::EnvelopeAggregate(L.VolumeShape) as BoundingRect,
		   max(L.VolumeShape.STDimension()) as MaxDim,
		   min(L.Z) as MinZ,
		   max(L.Z) as MaxZ
	FROM Location L group by L.ParentID) L  ON L.ParentID = S.ID
	INNER JOIN (Select ParentID from INSERTED I group by ParentID) I ON I.ParentID = S.ID /*Must use Group by in the inner join or we create many rows and re-run expensive functions calculating columns for all of them*/
	
	

	/*
	UPDATE SSC 
	SET
		SSC.BoundingRect = L.BoundingRect,
		SSC.Area = L.Area,
		SSC.Volume = L.Volume,
		SSC.MaxDim = L.MaxDim,
		SSC.MinZ = L.MinZ,
		SSC.MaxZ = L.MaxZ,
		SSC.ConvexHull = L.ConvexHull,
		SSC.LastModified = dbo.ufnLastStructureMorphologyModification(L.ID)
	FROM StructureSpatialCache as SSC
	 INNER JOIN 
		(select L.ParentID as ID, 
		   --Geometry::UnionAggregate(L.VolumeShape) as AggregateShape,
		   Geometry::ConvexHullAggregate(L.VolumeShape) as ConvexHull,
		   Geometry::EnvelopeAggregate(L.VolumeShape) as BoundingRect,
		   max(L.VolumeShape.STDimension()) as MaxDim,
		   min(L.Z) as MinZ, 
		   max(L.Z) as MaxZ
		 FROM Location L group by L.ParentID) AS L  ON L.ID = I.ID
     INNER JOIN (Select ID from Inserted) as I ON I.ID = 
	WHERE L.ID in (Select ID from Inserted)
	*/
	/*
	  WHEN MATCHED AND (Select Count(ID) from Location L where L.ParentID = SSC.ID) > 0
	  THEN
		 UPDATE SET (BoundingRect, Area, Volume, MaxDim, MinZ, MaxZ, ConvexHull, LastModified) 
		 from (select L.ParentID, 
		   --Geometry::UnionAggregate(L.VolumeShape) as AggregateShape,
		   Geometry::ConvexHullAggregate(L.VolumeShape) as ConvexHull,
		   Geometry::EnvelopeAggregate(L.VolumeShape) as BoundingRect,
		   max(L.VolumeShape.STDimension()) as MaxDim,
		   min(L.Z) as MinZ, 
		   max(L.Z) as MaxZ
	FROM Location L group by L.ParentID) L  ON L.ParentID = S.ID


	  WHEN NOT MATCHED BY SOURCE 
		then Delete from StructureSpatialCache SSC where SSC.ID = @ID
	SET
		SSC.BoundingRect = S.BoundingRect
		dbo.ufnStructureArea(S.ID) as Area, 
		dbo.ufnStructureVolume(S.ID) as Volume, 
		L.MaxDim as MaxDimension,
		L.MinZ as MinZ, 
		L.MaxZ as MaxZ,
		L.ConvexHull as ConvexHull,
		dbo.ufnLastStructureMorphologyModification(S.ID) as LastModified

	FROM Structure S
	INNER JOIN 
		(select L.ParentID, 
		   --Geometry::UnionAggregate(L.VolumeShape) as AggregateShape,
		   Geometry::ConvexHullAggregate(L.VolumeShape) as ConvexHull,
		   Geometry::EnvelopeAggregate(L.VolumeShape) as BoundingRect,
		   max(L.VolumeShape.STDimension()) as MaxDim,
		   min(L.Z) as MinZ, 
		   max(L.Z) as MaxZ
	FROM Location L group by L.ParentID) L  ON L.ParentID = S.ID
	*/
END