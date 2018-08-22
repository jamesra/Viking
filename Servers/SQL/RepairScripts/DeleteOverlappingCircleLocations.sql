IF OBJECT_ID (N'dbo.vOverlappingCircleLocationsForSameChildStructure', N'view') IS NOT NULL
    DROP VIEW vOverlappingCircleLocationsForSameChildStructure;
GO

CREATE VIEW vOverlappingCircleLocationsForSameChildStructure
AS
	--Find locations that overlap themselves
	select L1.ParentID, L1.ID, L2.ID as L2ID, L1.Radius, L1.Created, L1.VolumeX, L1.VolumeY, L1.Z
	from Location L1
	INNER JOIN Location L2 ON L1.ParentID = L2.ParentID AND L1.Z = L2.Z AND L1.ID != L2.ID
	INNER JOIN Structure S ON S.ID = L1.ParentID 
	where L1.MosaicShape.STIntersects(L2.MosaicShape) = 1 AND S.ParentID IS NOT NULL AND L1.TypeCode = 1 AND L2.TypeCode = 1
GO


select * from vOverlappingCircleLocationsForSameChildStructure
order by ParentID, Z

select ParentID, Z, MIN(Radius) 
FROM vOverlappingCircleLocationsForSameChildStructure
Group BY ParentID, Z

GO

delete LL from LocationLink LL
INNER JOIN vOverlappingCircleLocationsForSameChildStructure v ON v.ID = LL.A OR v.ID = LL.B
WHERE v.Radius <= 16

delete L from Location L 
INNER JOIN vOverlappingCircleLocationsForSameChildStructure v ON v.ID = L.ID
WHERE v.Radius <= 16

GO

IF OBJECT_ID (N'dbo.vOverlappingCircleLocationsForSameChildStructure', N'view') IS NOT NULL
    DROP VIEW vOverlappingCircleLocationsForSameChildStructure;
GO