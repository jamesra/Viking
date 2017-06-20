ALTER VIEW dbo.StructureSpatialView
AS
SELECT        S.ID as ID,
		      S.TypeID as TypeID,
			  S.ParentID as ParentID,
              dbo.ufnStructureArea(S.ID) as Area, 
              dbo.ufnStructureVolume(S.ID) as Volume, 
			  --sum(L.MosaicShape.STLength()) * dbo.XYScale() * dbo.ZScale() as Area,
			  --sum(L.MosaicShape.STArea()) * dbo.XYScale() * dbo.ZScale() as Volume,
			  --L.AggregateShape as AggregateShape,
			  L.ConvexHull as ConvexHull,
			  L.BoundingBox as BoundingBox,
			  --min(L.VolumeX) as MinX,
			  --max(L.VolumeX) as MaxX,
			  --min(L.VolumeY) as MinY,
			  --max(L.VolumeY) as MaxY,
			  L.MinZ as MinZ, 
			  L.MaxZ as MaxZ
FROM            [dbo].[Structure] S
INNER JOIN 
	(select L.ParentID, 
	   --Geometry::UnionAggregate(L.VolumeShape) as AggregateShape,
	   Geometry::ConvexHullAggregate(L.VolumeShape) as ConvexHull,
	   Geometry::EnvelopeAggregate(L.VolumeShape) as BoundingBox,
			  --min(L.VolumeX) as MinX,
			  --max(L.VolumeX) as MaxX,
			  --min(L.VolumeY) as MinY,
			  --max(L.VolumeY) as MaxY,
	   min(L.Z) as MinZ, 
	   max(L.Z) as MaxZ
FROM Location L group by L.ParentID) L  ON L.ParentID = S.ID
go  

ALTER VIEW [dbo].[StructureSpatialView] add constraint ID_PK_Entity_Framework_Hint primary key(ID) disable

CREATE UNIQUE CLUSTERED INDEX IDX_STRUCTURESPATIALVIEW_ID ON dbo.StructureSpatialView (ID)
CREATE CLUSTERED INDEX IDX_STRUCTURESPATIALVIEW_TypeID ON dbo.StructureSpatialView (TypeID)
CREATE CLUSTERED INDEX IDX_STRUCTURESPATIALVIEW_ParentID ON dbo.StructureSpatialView (ParentID)

select * from StructureSpatialView where ParentID = 476