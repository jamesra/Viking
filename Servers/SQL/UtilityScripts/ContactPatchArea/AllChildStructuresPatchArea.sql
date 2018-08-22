IF OBJECT_ID('tempdb..#PSDs') IS NOT NULL DROP TABLE #PSDs
IF OBJECT_ID('tempdb..#ExtraStructureLinks') IS NOT NULL DROP TABLE #ExtraStructureLinks
IF OBJECT_ID('tempdb..#ContactDuplicates') IS NOT NULL DROP TABLE #ContactDuplicates
IF OBJECT_ID('tempdb..#PairDistance') IS NOT NULL DROP TABLE #PairDistance
IF OBJECT_ID('tempdb..#NearestPartner') IS NOT NULL DROP TABLE #NearestPartner
IF OBJECT_ID('tempdb..#PairsToDelete') IS NOT NULL DROP TABLE #PairsToDelete

select T.ID as LocID, T.ParentID as ParentID, T.Radius as Radius, T.MosaicShape as MosaicShape, T.Z as Z,
	   T.MosaicShape.STDimension() as Dimension, ParentStruct.Label as TargetLabel,
	    case 
			when T.MosaicShape.STDimension() = 2 then
				NULL
	        when T.MosaicShape.STDimension() = 1 then
				T.MosaicShape
			ELSE
				NULL
			end
		as Line
INTO #PSDs
from Location T
join Structure Struct ON Struct.ID = T.ParentID
join Structure ParentStruct ON ParentStruct.ID = Struct.ParentID
--where S.ParentID = 1452 OR S.ParentID = 1468 OR S.ParentID = 1746 OR S.ParentID = 1338 OR S.ParentID = 862
where Struct.ParentID = 606
order by Struct.ParentID

GO
 

--Renders the first 1000 shapes in the spatial results view
--IF OBJECT_ID('tempdb..#GAggregate') IS NOT NULL DROP TABLE #GAggregate
--select SParentID as SParentID, SMosaicShape as Shape into #GAggregate from #StructureLinks where SMosaicShape is NOT NULL
--insert into #GAggregate (SParentID, Shape) Select SParentID, TMosaicShape from #StructureLinks where TMosaicShape is NOT NULL
--insert into #GAggregate (SParentID, Shape) Select SParentID, Line from #StructureLinks where Line IS NOT NULL

--select SParentID, Shape from #GAggregate where Shape is NULL OR Shape.STIsValid() = 0

--select TOP 1000 SParentID, Shape as Aggregate from #GAggregate order by SParentID
--select TOP 1000 SParentID, geometry::CollectionAggregate(Shape) as Aggregate from #GAggregate group by SParentID order by SParentID

GO

declare @AreaScalar float
--Measures the area of the PSD
set @AreaScalar = dbo.XYScale() * dbo.ZScale()
select PSD.ParentID as TargetParentID,
	   S.TypeID as TypeID, sum( Line.STLength()) * @AreaScalar as Area_nm,
	   sum( Line.STLength()) * @AreaScalar / 1000000.0 as Area_um,
	   count(PSD.LocID) as NumLocations, TargetLabel  --, geometry::CollectionAggregate(Shape) as shape 
from #PSDs PSD
	join Structure S ON S.ID = PSD.ParentID 
	group by PSD.ParentID, TypeID, TargetLabel
	order by TypeID