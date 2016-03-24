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

IF OBJECT_ID ('dbo.vNearestParentStructureLocation', 'view') IS NOT NULL
	DROP VIEW vNearestParentStructureLocation
GO

IF OBJECT_ID ('dbo.vParentStructureLocationDistance', 'view') IS NOT NULL
	DROP VIEW vParentStructureLocationDistance
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
	select L.ParentID as ChildStructureID, L.ID as ChildLocationID, 
	   PL.ParentID as ParentStructureID, PL.ID as ParentLocationID
	from dbo.Location L
	
	INNER JOIN dbo.Structure S ON S.ID = L.ParentID
	INNER JOIN dbo.Structure PS ON S.ParentID = PS.ID
	INNER JOIN dbo.Location PL ON PL.ParentID = PS.ID AND PL.Z = L.Z
	INNER JOIN dbo.vParentStructureLocationDistance D ON D.ChildLocationID = L.ID --Moving the subquery to a view took a 5 minute query to 3 minutes
	WHERE L.MosaicShape.STDistance(PL.MosaicShape) = D.MinDistance 
GO