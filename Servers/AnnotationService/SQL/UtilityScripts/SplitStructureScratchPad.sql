IF OBJECT_ID('tempdb..#LocationLinkPool') IS NOT NULL DROP TABLE #LocationLinkPool
IF OBJECT_ID('tempdb..#LocationsInKeepSubGraph') IS NOT NULL DROP TABLE #LocationsInKeepSubGraph
IF OBJECT_ID('tempdb..#LocationsInSplitSubGraph') IS NOT NULL DROP TABLE #LocationsInSplitSubGraph
IF OBJECT_ID('tempdb..#ChildStructureLocations') IS NOT NULL DROP TABLE #ChildStructureLocations
IF OBJECT_ID('tempdb..#StructureLocations') IS NOT NULL DROP TABLE #StructureLocations
IF OBJECT_ID('tempdb..#DistanceToEachStructure') IS NOT NULL DROP TABLE #DistanceToEachStructure
IF OBJECT_ID('tempdb..#DistanceToNearestStructure') IS NOT NULL DROP TABLE #DistanceToNearestStructure
IF OBJECT_ID('tempdb..#ParentIDForChildStructure') IS NOT NULL DROP TABLE #ParentIDForChildStructure

DECLARE @KeepStructureID bigint, @SplitStructureID bigint
DECLARE @FirstLocationIDOfSplitStructure bigint
DECLARE @A bigint, @B bigint
set @KeepStructureID = 439
set @SplitStructureID = 0
set @FirstLocationIDOfSplitStructure = 35539

SELECT A,B into #LocationLinkPool from dbo.StructureLocationLinks(@KeepStructureID) order by A

--select * from #LocationLinkPool where A = @FirstLocationIDOfSplitStructure OR B = @FirstLocationIDOfSplitStructure

CREATE TABLE #LocationsInSplitSubGraph(ID bigint)
insert into #LocationsInSplitSubGraph (ID) values (@FirstLocationIDOfSplitStructure)
	  
--Loop over the pool adding to the subgraph until we cannot find any more locations
DECLARE @RowsAddedToSubgraph bigint
set @RowsAddedToSubgraph = 1
While @RowsAddedToSubgraph > 0
BEGIN
--insert into #GAggregate (SParentID, Shape) Select SParentID, TMosaicShape from #StructureLinks where TMosaicShape is NOT NULL

	insert into #LocationsInSplitSubGraph (ID) 
		Select B as ID from #LocationLinkPool where A in (select ID from #LocationsInSplitSubGraph)
		union 
		Select A as ID from #LocationLinkPool where B in (select ID from #LocationsInSplitSubGraph)

	set @RowsAddedToSubgraph = @@ROWCOUNT

	--select distinct(ID) from #LocationsInSplitSubGraph

	--Remove links we have already added
	delete LLP from #LocationLinkPool LLP
	join #LocationsInSplitSubGraph SA ON SA.ID = LLP.A
	join #LocationsInSplitSubGraph SB ON SB.ID = LLP.B
END


select ID into #LocationsInKeepSubGraph from Location where ParentID = @KeepStructureID AND ID not in (select ID from #LocationsInSplitSubGraph)

select VolumeShape, Z, KL.ID, @KeepStructureID as ParentID into  #StructureLocations
FROM Location L 
JOIN #LocationsInKeepSubGraph KL ON KL.ID = L.ID
UNION ALL
select VolumeShape, Z, SL.ID, @SplitStructureID as ParentID FROM Location L 
JOIN #LocationsInSplitSubGraph SL ON SL.ID = L.ID

select ParentID as StructureID, geometry::ConvexHullAggregate(VolumeShape) as Shape, MAX(Z) as Z 
    into #ChildStructureLocations from Location
	where ParentID in (select ID from Structure where ParentID = @KeepStructureID)
	group by ParentID

--select * from #StructureLocations
--select * from #ChildStructureLocations

--Find the nearest location in either the keep or split structure
select CSL.StructureID as StructureID, SL.ParentID as NewParentID, MIN(SL.VolumeShape.STDistance(CSL.Shape)) as Distance
	INTO #DistanceToEachStructure from #ChildStructureLocations CSL
	join #StructureLocations SL ON SL.Z = CSL.Z
	Group By CSL.StructureID, SL.ParentID 
	order by CSL.StructureID

select SL.StructureID as StructureID, MIN(SL.Distance) as Distance 
INTO #DistanceToNearestStructure from #DistanceToEachStructure SL
group by SL.StructureID

select * from #DistanceToEachStructure order by StructureID
--select * from #DistanceToNearestStructure

select SD.StructureID as StructureID, SD.NewParentID as NewParentID, SD.Distance as Distance
into #ParentIDForChildStructure from #DistanceToEachStructure SD
join #DistanceToNearestStructure SN ON SN.StructureID = SD.StructureID AND SN.Distance = SD.Distance

select * from #ParentIDForChildStructure
/*
update Structure set ParentID = PCS.NewParentID 
FROM Structure S
	JOIN #ParentIDForChildStructure PCS ON S.ID = PCS.StructureID
	*/