--Cases for locations
--A: Locations whose structure is linked to a partner
-- 1. The source and target structure both have one link
--	a. Overlapping annotations
--   i. Ribbon (Perpendicular to PSD)
--   ii. Conventional/Gap (Parallel to PSD)
--  b. Non-overlapping annotations
--   i. Ribbon (Perpendicular to PSD)
--		Pre:  Create a perpendicular line to the PSD center for the ribbon. 
--		Post: Create a parallel line to the Ribbon 
--   ii. Conventional/Gap (Parallel to PSD)
--		Pre:  Create a parallel line for the Post-structure
--		Post: Create a parallel line to the Pre-structure
-- 2. The source has multiple target locations
--  a. Overlapping annotations
--		Average the target locations to create a single target geometry, then use code for Overlapping annotations
--	b. Non-overlapping annotations
--		Average the target locations to create a single target geometry, then use code for Non-overlapping annotations
-- 3. The target has multiple input source locations
--  a. Overlapping annotations
--		Average the target locations to create a single source geometry, then use code for Overlapping annotations
--	b. Non-overlapping annotations
--		Average the target locations to create a single source geometry, then use code for Non-overlapping annotations
--B: Locations whose structure is not linked
--	i.  Ribbons (Parallel line to structure's parent)
--  ii. Non-Ribbons (Perpendicular line to structure's parent)

USE TEST
GO

IF OBJECT_ID('vLinkedStructureLocations', 'view') IS NOT NULL DROP VIEW vLinkedStructureLocations
GO

CREATE VIEW vLinkedStructureLocations
as 
	
	select SLoc.ID as SourceLocID, SLoc.ParentID as SourceID, SourceStruct.TypeID as SourceTypeID,
		   TLoc.ID as TargetLocID, TLoc.ParentID as TargetID, TargetStruct.TypeID as TargetTypeID,
		   SL.Username as Username, SL.Created as Created, SLoc.Z as Z, 
		   SLoc.MosaicShape.STDimension() as Dimension, SLoc.MosaicShape.STDistance(TLoc.MosaicShape) as Distance
	from Location SLoc
	join StructureLink SL ON SL.SourceID = SLoc.ParentID
	join Location TLoc ON SL.TargetID = TLoc.ParentID AND TLoc.Z = SLoc.Z  
	join Structure SourceStruct ON SourceStruct.ID = SLoc.ParentID
	join Structure TargetStruct ON TargetStruct.ID = TLoc.ParentID
GO

IF OBJECT_ID('vUnlinkedLocations') IS NOT NULL DROP VIEW vUnlinkedLocations
GO

CREATE VIEW vUnlinkedLocations WITH SCHEMABINDING
AS 
	-- Locations that are part of a child structure which is not part of a structure link
	select L.ID, L.ParentID as StructureID, L.MosaicShape as MosaicShape, S.ParentID as StructuresParentID, S.TypeID as TypeID
			 
	from Location L
		JOIN Structure S on S.ID = L.ParentID
		where S.ParentID IS NOT NULL AND S.ID not in ((select SourceID from StructureLink) UNION (Select TargetID from StructureLink))

GO

IF OBJECT_ID ('dbo.vNearestParentStructureLocation', 'view') IS NOT NULL
	DROP VIEW vNearestParentStructureLocation
GO

CREATE VIEW vNearestParentStructureLocation WITH SCHEMABINDING
AS
	--Return the nearest location on our parent structure
	select L.ParentID as ChildStructureID, L.ID as ChildLocationID, L.Z, 
	   PL.ParentID as ParentStructureID, PL.ID as ParentLocationID
	from dbo.Location L
	
	INNER JOIN dbo.Structure S ON S.ID = L.ParentID
	INNER JOIN dbo.Structure PS ON S.ParentID = PS.ID
	INNER JOIN dbo.Location PL ON PL.ParentID = PS.ID AND PL.Z = L.Z
	INNER JOIN dbo.vParentStructureLocationDistance D ON D.ChildLocationID = L.ID --Moving the subquery to a view took a 5 minute query to 3 minutes
	WHERE L.MosaicShape.STDistance(PL.MosaicShape) = D.MinDistance 

GO

CREATE UNIQUE CLUSTERED INDEX IDX_vNearestParentStructureLocation_ChildLocationID 
	ON vNearestParentStructureLocation (ChildLocationID)

IF OBJECT_ID('vLinkedLocationWithoutSectionPartner', 'view') IS NOT NULL DROP VIEW vLinkedLocationWithoutSectionPartner
GO

CREATE VIEW vLinkedLocationWithoutSectionPartner
as 
	--Return all locations whose structure is linked but the partner in the link does not have a location on our section
	SELECT L.ID AS ID, S.TypeID 
	From Location L 
	INNER JOIN Structure S ON S.ID = L.ParentID
	WHERE L.ID NOT IN (
		select vUL.ID FROM vUnlinkedLocations vUL
		UNION
		select vLSLS.SourceLocID FROM vLinkedStructureLocations vLSLS
		UNION
		select vLSLT.TargetLocID FROM vLinkedStructureLocations vLSLT
		)
	AND S.ParentID IS NOT NULL
GO

IF OBJECT_ID('vStructureLinkLocationPairs', 'view') IS NOT NULL DROP VIEW vStructureLinkLocationPairs
GO

CREATE VIEW vStructureLinkLocationPairs
as 
	--Count the number of Source->Target location pairs for a link on each section
	Select vLSL.SourceLocID, vLSL.SourceID, vLSL.SourceTypeID, 
	       vLSL.TargetLocID, vLSL.TargetID, vLSL.TargetTypeID,
		   SPairCount.NumPairs as NumSourceLocationsForLink, 
		   TPairCount.NumPairs as NumTargetLocationsForLink
	from vLinkedStructureLocations vLSL
	INNER JOIN (select SourceLocID, count(TargetLocID) as NumPairs
				from vLinkedStructureLocations
				group by SourceLocID
				) SPairCount ON SPairCount.SourceLocID = vLSL.SourceLocID
	INNER JOIN (select TargetLocID, count(SourceLocID) as NumPairs
				from vLinkedStructureLocations
				group by TargetLocID
				) TPairCount ON TPairCount.TargetLocID = vLSL.TargetLocID
GO

--Create a temp table to store all of the new shapes for affected locations
IF OBJECT_ID('tempdb..#LinkedStructureLocationsLines') IS NOT NULL DROP TABLE #LinkedStructureLocationsLines

CREATE TABLE #LocationLines(x INT PRIMARY KEY, TypeCode INT, MosaicShape geometry)



--------------------------- Perpendicular lines for unlinked locations (RIBBON) update -----------------------------

update L SET TypeCode = 5, 
					MosaicShape = dbo.ufnLineFromAngleAndDistance(dbo.ufnAngleBetweenShapes(Ribbon.MosaicShape, Parent.MosaicShape),
									   Ribbon.Radius * 2,
									   Ribbon.MosaicShape.STCentroid())
	FROM Location L
		JOIN vUnlinkedLocations UL ON UL.ID = L.ID
		JOIN vNearestParentStructureLocation NP ON UL.ID = NP.ChildLocationID
		JOIN Location Ribbon ON Ribbon.ID = NP.ChildLocationID
		JOIN Location Parent ON Parent.ID = NP.ParentLocationID
	 where UL.TypeID = 73 AND L.TypeCode = 1 AND Ribbon.MosaicShape.STDimension() > 1 and Parent.MosaicShape.STDimension() > 1

GO
--------------------------- Parallel lines for unlinked locations (Gap Junction & PSD) update -----------------------------

update L SET TypeCode = 5, 
					MosaicShape = dbo.ufnLineFromAngleAndDistance(dbo.ufnAngleBetweenShapes(Ribbon.MosaicShape, Parent.MosaicShape) + PI() / 2.0,
									   Ribbon.Radius * 2,
									   Ribbon.MosaicShape.STCentroid())
	FROM Location L
		JOIN vUnlinkedLocations UL ON UL.ID = L.ID
		JOIN vNearestParentStructureLocation NP ON UL.ID = NP.ChildLocationID
		JOIN Location Ribbon ON Ribbon.ID = NP.ChildLocationID
		JOIN Location Parent ON Parent.ID = NP.ParentLocationID
	 where (	 UL.TypeID = 28 OR -- Gap Junction
				 UL.TypeID = 35 OR -- PSD
				 UL.TypeID = 189 OR -- BC Conventional Synapse
				 UL.TypeID = 240 OR -- Plaque-like Pre
				 UL.TypeID = 241 OR -- Plaque-line Post
				 UL.TypeID = 85 ) --Adherens
			AND L.TypeCode = 1 AND Ribbon.MosaicShape.STDimension() > 1 and Parent.MosaicShape.STDimension() > 1

GO

-------------------------------------------------------
--Begin migration code for locations part of a structure link, but without a partner on the same section
-------------------------------------------------------

---------------------------  Perpendicular lines for linked locations without a partner on the section (RIBBON) update -----------------------------

update L SET TypeCode = 5, 
					MosaicShape = dbo.ufnLineFromAngleAndDistance(dbo.ufnAngleBetweenShapes(Ribbon.MosaicShape, Parent.MosaicShape),
									   Ribbon.Radius * 2,
									   Ribbon.MosaicShape.STCentroid())
	FROM Location L
		JOIN vLinkedLocationWithoutSectionPartner UL ON UL.ID = L.ID
		JOIN vNearestParentStructureLocation NP ON UL.ID = NP.ChildLocationID
		JOIN Location Ribbon ON Ribbon.ID = NP.ChildLocationID
		JOIN Location Parent ON Parent.ID = NP.ParentLocationID
	 where UL.TypeID = 73 AND L.TypeCode = 1 AND Ribbon.MosaicShape.STDimension() > 1 and Parent.MosaicShape.STDimension() > 1

GO
---------------------------  Perpendicular lines for linked locations without a partner on the section (Gap Junction & PSD) update -----------------------------

update L SET TypeCode = 5, 
					MosaicShape = dbo.ufnLineFromAngleAndDistance(dbo.ufnAngleBetweenShapes(Ribbon.MosaicShape, Parent.MosaicShape) + PI() / 2.0,
									   Ribbon.Radius * 2,
									   Ribbon.MosaicShape.STCentroid())
	FROM Location L
		JOIN vLinkedLocationWithoutSectionPartner UL ON UL.ID = L.ID
		JOIN vNearestParentStructureLocation NP ON UL.ID = NP.ChildLocationID
		JOIN Location Ribbon ON Ribbon.ID = NP.ChildLocationID
		JOIN Location Parent ON Parent.ID = NP.ParentLocationID
	 where (	 UL.TypeID = 28 OR -- Gap Junction
				 UL.TypeID = 35 OR -- PSD
				 UL.TypeID = 189 OR -- BC Conventional Synapse
				 UL.TypeID = 240 OR -- Plaque-like Pre
				 UL.TypeID = 241 OR -- Plaque-line Post
				 UL.TypeID = 85 ) --Adherens
			AND L.TypeCode = 1 AND Ribbon.MosaicShape.STDimension() > 1 and Parent.MosaicShape.STDimension() > 1

GO
