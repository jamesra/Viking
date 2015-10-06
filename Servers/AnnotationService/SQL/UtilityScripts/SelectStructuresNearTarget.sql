
IF OBJECT_ID('tempdb..#CONVEXHULLS') IS NOT NULL DROP TABLE #CONVEXHULLS

select ParentID, geometry::ConvexHullAggregate( VolumeShape ) as CV, count(ID) as NumLocs INTO #CONVEXHULLS
from Location where Z>= 175 and
		ParentID in (Select ID from Structure where TypeID = 1)
group by ParentID


	IF OBJECT_ID('tempdb..#ChildStructure') IS NOT NULL
	BEGIN
		DROP TABLE #ChildStructure
	END
	IF OBJECT_ID('tempdb..#LinkedStructures') IS NOT NULL
	BEGIN
		DROP TABLE #LinkedStructures
	END
	IF OBJECT_ID('tempdb..#LinkedCells') IS NOT NULL
	BEGIN
		DROP TABLE #LinkedCells
	END

declare @CenterID int
set @CenterID = 595

select ID into #ChildStructure from structure where ParentID = @CenterID
select SourceID, TargetID into #LinkedStructures 
	from StructureLink where 
		SourceID in (Select ID from #ChildStructure) 
			or
		TargetID in (Select ID from #CHildStructure)
				 
select distinct ParentID as ID into #LinkedCells from Structure 
	where ID in (select SourceID from #LinkedStructures) or ID in (select TargetID from #LinkedStructures)

--Once #Convex Hulls is populated you can highlight the text below to quickly filter the table--
declare @center geometry
set @center = (select geometry::ConvexHullAggregate( VolumeShape ) from Location where ParentID=@CenterID)

select ParentID, CV from #CONVEXHULLS
		where (@center.STIntersection(CV).STArea() / @center.STArea() < 0.25 and
			NumLocs > 10 and @center.STCentroid().STDistance(CV.STCentroid()) < 30000 and
			CV.STArea() > @center.STArea() * 0.50 and
			CV.STArea() < @center.STArea() * 1.50 and 
			ParentID in (select ID from Structure where LEFT(Label, 2) = 'CB') AND
			ParentID not in (Select ID from #LinkedCells)) 
			OR
			ParentID = @CenterID
