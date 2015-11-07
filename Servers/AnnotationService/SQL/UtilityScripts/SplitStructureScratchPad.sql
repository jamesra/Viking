IF OBJECT_ID('tempdb..#LocationLinkPool') IS NOT NULL DROP TABLE #LocationLinkPool
IF OBJECT_ID('tempdb..#LocationsInKeepSubGraph') IS NOT NULL DROP TABLE #LocationsInKeepSubGraph
IF OBJECT_ID('tempdb..#LocationsInSplitSubGraph') IS NOT NULL DROP TABLE #LocationsInSplitSubGraph

DECLARE @KeepStructureID bigint, @SplitStructureID bigint
DECLARE @FirstLocationIDOfSplitStructure bigint
DECLARE @A bigint, @B bigint
set @KeepStructureID = 439
set @FirstLocationIDOfSplitStructure = 35539

SELECT A,B into #LocationLinkPool from dbo.StructureLocationLinks(@KeepStructureID) order by A

select * from #LocationLinkPool where A = @FirstLocationIDOfSplitStructure OR B = @FirstLocationIDOfSplitStructure

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

select distinct(ID) from #LocationsInSplitSubGraph
select distinct(ID) from #LocationsInKeepSubGraph

select ParentID as StructureID, geometry::ConvexHullAggregate(VolumeShape).STCentroid() as Shape from Location
	where ParentID in (select ID from Structure where ParentID = @KeepStructureID)
	group by ParentID
