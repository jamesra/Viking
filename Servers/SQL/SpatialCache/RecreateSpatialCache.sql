DELETE StructureSpatialCache  

INSERT INTO StructureSpatialCache
SELECT        S.ID as ID,  
				L.BoundingRect as BoundingRect,
				[dbo].ufnStructureArea(S.ID) as Area, 
				[dbo].ufnStructureVolume(S.ID) as Volume, 
				L.MaxDim as MaxDimension,
				L.MinZ as MinZ, 
				L.MaxZ as MaxZ,
				L.ConvexHull as ConvexHull,
				[dbo].ufnLastStructureMorphologyModification(S.ID) as LastModified

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