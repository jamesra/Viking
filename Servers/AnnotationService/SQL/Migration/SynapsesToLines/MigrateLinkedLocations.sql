--Cases for linked locations
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


/*
--Take care of basic cases where there are not multiple links
select vLSL.SourceLocID, vLSL.SourceID, vLSL.SourceTypeID, 
	   vLSL.TargetLocID, vLSL.TargetID, vLSL.TargetTypeID, SPairCount.NumPairs as SNumPairs, 
	   TPairCount.NumPairs as TNumPairs,
	   case 
		when vLSL.SourceTypeID = 73 then
			--Ribbon 
			dbo.ufnPerpendicularLineForLinkedShapes(SL.MosaicShape, TL.MosaicShape)
		ELSE 			
			dbo.ufnParallelLineForLinkedShapes(SL.MosaicShape, TL.MosaicShape)
		END as OutputLine
		
from vLinkedStructureLocations vLSL
INNER JOIN (select SourceLocID, count(TargetLocID) as NumPairs
			from vLinkedStructureLocations
			group by SourceLocID
			) SPairCount ON SPairCount.SourceLocID = vLSL.SourceLocID
INNER JOIN (select TargetLocID, count(SourceLocID) as NumPairs
			from vLinkedStructureLocations
			group by TargetLocID
			) TPairCount ON TPairCount.TargetLocID = vLSL.TargetLocID
INNER JOIN Location SL ON SL.ID = SPairCount.SourceLocID
INNER JOIN Location TL ON TL.ID = TPairCount.TargetLocID
WHERE TPairCount.NumPairs = 1 AND SPairCount.NumPairs = 1 AND SL.TypeCode = 1 AND SL.Z = 250
*/
IF OBJECT_ID('tempdb..#LinkedStructureLocationsLines') IS NOT NULL DROP TABLE #LinkedStructureLocationsLines

Select vLSL.SourceLocID as ID, 
	   case 
		when vLSL.SourceTypeID = 73 then
			--Ribbon 
			dbo.ufnPerpendicularLineForLinkedShapes(SL.MosaicShape, TL.MosaicShape)
		ELSE 			
			dbo.ufnParallelLineForLinkedShapes(SL.MosaicShape, TL.MosaicShape)
		END as OutputLine
into #LinkedStructureLocationsLines
from vLinkedStructureLocations vLSL
INNER JOIN (select SourceLocID, count(TargetLocID) as NumPairs
			from vLinkedStructureLocations
			group by SourceLocID
			) SPairCount ON SPairCount.SourceLocID = vLSL.SourceLocID
INNER JOIN (select TargetLocID, count(SourceLocID) as NumPairs
			from vLinkedStructureLocations
			group by TargetLocID
			) TPairCount ON TPairCount.TargetLocID = vLSL.TargetLocID
INNER JOIN Location SL ON SL.ID = SPairCount.SourceLocID
INNER JOIN Location TL ON TL.ID = TPairCount.TargetLocID
WHERE TPairCount.NumPairs = 1 AND SPairCount.NumPairs = 1 AND SL.TypeCode = 1
		
--Collect all cases of targets that get input from a single source
INSERT into #LinkedStructureLocationsLines (ID,OutputLine) 
Select vLSL.TargetLocID as ID, dbo.ufnParallelLineForLinkedShapes(TL.MosaicShape, SL.MosaicShape) as OutputLine
from vLinkedStructureLocations vLSL
INNER JOIN (select SourceLocID, count(TargetLocID) as NumPairs
			from vLinkedStructureLocations
			group by SourceLocID
			) SPairCount ON SPairCount.SourceLocID = vLSL.SourceLocID
INNER JOIN (select TargetLocID, count(SourceLocID) as NumPairs
			from vLinkedStructureLocations
			group by TargetLocID
			) TPairCount ON TPairCount.TargetLocID = vLSL.TargetLocID
INNER JOIN Location SL ON SL.ID = SPairCount.SourceLocID
INNER JOIN Location TL ON TL.ID = TPairCount.TargetLocID
WHERE TPairCount.NumPairs = 1 AND SL.TypeCode = 1
		
select * from #LinkedStructureLocationsLines

UPDATE L SET MosaicShape = LSLL.OutputLine, TypeCode = 5
from Location L
INNER JOIN #LinkedStructureLocationsLines LSLL ON LSLL.ID = L.ID
 
GO

--Above here works
------------------------------------------


--Find the cases of one source, multiple targets, where the source only intersects one target.
--Use this single intersecting target to define a new line for the source
--1/14/16  This case only catches 8 cases... but it took a lot of time and works so I'm using it.
select vLSL.*, SourceIntersectionCounts.NumIntersects as NumIntersects,
		case 
		when vLSL.SourceTypeID = 73 then
			--Ribbon 
			dbo.ufnPerpendicularLineForLinkedShapes(SL.MosaicShape, TL.MosaicShape)
		ELSE 			
			dbo.ufnParallelLineForLinkedShapes(SL.MosaicShape, TL.MosaicShape)
		END as OutputLine
from vLinkedStructureLocations vLSL
INNER JOIN ( --Count the number of intersecting target locations for each source location
	select vLinkLocationPairs.SourceLocID, SUM(CONVERT(int,SL.MosaicShape.STIntersects(TL.MosaicShape))) as NumIntersects
	from vStructureLinkLocationPairs vLinkLocationPairs
	INNER JOIN Location SL ON SL.ID = vLinkLocationPairs.SourceLocID
	INNER JOIN Location TL ON TL.ID = vLinkLocationPairs.TargetLocID 
	group by vLinkLocationPairs.SourceLocID
	) SourceIntersectionCounts ON SourceIntersectionCounts.SourceLocID = vLSL.SourceID
INNER JOIN (select SourceLocID, count(TargetLocID) as NumPairs
			from vLinkedStructureLocations
			group by SourceLocID
			) SPairCount ON SPairCount.SourceLocID = vLSL.SourceLocID
INNER JOIN Location SL ON SL.ID = vLSL.SourceLocID
INNER JOIN Location TL ON TL.ID = vLSL.TargetLocID
	WHERE SourceIntersectionCounts.NumIntersects = 1 AND 
		  SPairCount.NumPairs > 1 AND
		  SL.MosaicShape.STIntersects(TL.MosaicShape) = 1 --Choose the TargetLocation intersecting with the source location




--Find the cases of one source, multiple targets, where the source only intersects one target.
--Use this single intersecting target to define a new line for the source
--1/14/16  This case only catches 8 cases... but it took a lot of time and works so I'm using it.
INSERT into #LinkedStructureLocationsLines (ID,OutputLine) 
select vLSL.SourceID,
		case 
		when vLSL.SourceTypeID = 73 then
			--Ribbon 
			dbo.ufnPerpendicularLineForLinkedShapes(SL.MosaicShape, TL.MosaicShape)
		ELSE 			
			dbo.ufnParallelLineForLinkedShapes(SL.MosaicShape, TL.MosaicShape)
		END as OutputLine
from vLinkedStructureLocations vLSL
INNER JOIN ( --Count the number of intersecting target locations for each source location
	select vLinkLocationPairs.SourceLocID, SUM(CONVERT(int,SL.MosaicShape.STIntersects(TL.MosaicShape))) as NumIntersects
	from vStructureLinkLocationPairs vLinkLocationPairs
	INNER JOIN Location SL ON SL.ID = vLinkLocationPairs.SourceLocID
	INNER JOIN Location TL ON TL.ID = vLinkLocationPairs.TargetLocID 
	group by vLinkLocationPairs.SourceLocID
	) SourceIntersectionCounts ON SourceIntersectionCounts.SourceLocID = vLSL.SourceID
INNER JOIN (select SourceLocID, count(TargetLocID) as NumPairs
			from vLinkedStructureLocations
			group by SourceLocID
			) SPairCount ON SPairCount.SourceLocID = vLSL.SourceLocID
INNER JOIN Location SL ON SL.ID = vLSL.SourceLocID
INNER JOIN Location TL ON TL.ID = vLSL.TargetLocID
	WHERE SourceIntersectionCounts.NumIntersects = 1 AND 
		  SPairCount.NumPairs > 1 AND
		  SL.MosaicShape.STIntersects(TL.MosaicShape) = 1 --Choose the TargetLocation intersecting with the source location

--Above here works, but needs verification checking in Viking
----------------------------------------------------------------

--Try to migrate cases where there are two locations the source location could link to on a section. 
--Use the one which we overlap with if there is only one
select SourceLocID, count(TargetLocID) as NumPairs
			from vLinkedStructureLocations 

--Find cases of one source, multiple targets
select vLSL.SourceLocID, vLSL.SourceID, vLSL.SourceTypeID, 
	   vLSL.TargetLocID, vLSL.TargetID, vLSL.TargetTypeID, SPairCount.NumPairs as SNumPairs, 
	   TPairCount.NumPairs as TNumPairs, vLSL.Username as Username, vLSL.Created as CreatedOn,
	   SL.MosaicShape.STIntersects(TL.MosaicShape) as Intersects,
	   SL.VolumeX, SL.VolumeY, SL.Z, 1 as Downsample
	   
from vLinkedStructureLocations vLSL
INNER JOIN (select SourceLocID, count(TargetLocID) as NumPairs
			from vLinkedStructureLocations
			group by SourceLocID
			) SPairCount ON SPairCount.SourceLocID = vLSL.SourceLocID
INNER JOIN (select TargetLocID, count(SourceLocID) as NumPairs
			from vLinkedStructureLocations
			group by TargetLocID
			) TPairCount ON TPairCount.TargetLocID = vLSL.TargetLocID
INNER JOIN Location SL ON SL.ID = SPairCount.SourceLocID
INNER JOIN Location TL ON TL.ID = TPairCount.TargetLocID
WHERE SPairCount.NumPairs > 1 AND TPairCount.NumPairs > 1 AND SL.TypeCode = 1


select vPairCount.*, SL.MosaicShape.STIntersects(TL.MosaicShape) as NumIntersects
from vStructureLinkLocationPairs vPairCount
INNER JOIN Location SL ON SL.ID = vPairCount.SourceLocID
INNER JOIN Location TL ON TL.ID = vPairCount.TargetLocID
WHERE vPairCount.NumSourceLocationsForLink > 1
order by vPairCount.SourceLocID

select * from vLinkLocationPairs vPairCount
