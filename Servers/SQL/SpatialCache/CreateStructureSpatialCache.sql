CREATE TABLE StructureSpatialCache
(
	ID bigint NOT NULL PRIMARY KEY CLUSTERED,
	BoundingRect Geometry NOT NULL,
	Area float NOT NULL CONSTRAINT StructureSpatialCache_Area_Default DEFAULT 0,
	Volume float NOT NULL CONSTRAINT StructureSpatialCache_Volume_Default DEFAULT 0,
	MaxDimension int NOT NULL CONSTRAINT StructureSpatialCache_MaxDimension_Default DEFAULT 0,
	MinZ float NOT NULL,
	MaxZ float NOT NULL,
	ConvexHull Geometry NOT NULL,
	LastModified DateTime NOT NULL
	FOREIGN KEY (ID) REFERENCES [Structure] (ID) ON UPDATE NO ACTION ON DELETE CASCADE
)

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
