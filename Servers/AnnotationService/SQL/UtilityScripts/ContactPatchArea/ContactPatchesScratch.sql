IF OBJECT_ID('tempdb..#StructureLinks') IS NOT NULL DROP TABLE #StructureLinks
IF OBJECT_ID('tempdb..#ExtraStructureLinks') IS NOT NULL DROP TABLE #ExtraStructureLinks
IF OBJECT_ID('tempdb..#ContactDuplicates') IS NOT NULL DROP TABLE #ContactDuplicates
IF OBJECT_ID('tempdb..#PairDistance') IS NOT NULL DROP TABLE #PairDistance
IF OBJECT_ID('tempdb..#NearestPartner') IS NOT NULL DROP TABLE #NearestPartner
IF OBJECT_ID('tempdb..#PairsToDelete') IS NOT NULL DROP TABLE #PairsToDelete

select S.ID as SID, S.ParentID as SParentID, S.Radius as SRadius, S.MosaicShape as SMosaicShape, S.Z as SZ,
	   T.ID as TID, T.ParentID as TParentID, T.Radius as TRadius, T.MosaicShape as TMosaicShape, T.Z as TZ ,
	   S.MosaicShape.STDimension() as Dimension,
	    case 
			when S.MosaicShape.STDimension() = 2 then
				dbo.ufnLineFromLinkedShapes(S.MosaicShape, T.MosaicShape)
	        when S.MosaicShape.STDimension() = 1 then
				T.MosaicShape
			ELSE
				NULL
			end
		as Line
INTO #StructureLinks
from Location S
join StructureLink L ON L.SourceID = S.ParentID
join Location T      ON L.TargetID = T.ParentID AND T.Z = S.Z
--where S.ParentID = 1452 OR S.ParentID = 1468 OR S.ParentID = 1746 OR S.ParentID = 1338 OR S.ParentID = 862
--where S.ParentID = 862
order by S.ParentID, T.ParentID

GO

--Count the cases where we have two locations for the same structure on the same Z section that create extra fake contact patches.
--NOTE: This code breaks if the Synapse overlaps with BOTH PSD pairs.  There's no fixing it.  Just ensure the synapse only overlaps with the correct PSD and not the more distant one.
/*
select SParentID, TParentID, SZ, NumPairs
into #ExtraStructureLinks
from (Select SParentID, TParentID, SZ, Count(SParentID) as NumPairs
	  from #StructureLinks
	  group by SParentID, TParentID, SZ) as ExtraStructureLinks
where NumPairs > 1 order by SParentID
 
select SL.SID, SL.TID, SMosaicShape.STDistance(TMosaicShape) as Distance
into #PairDistance
from #StructureLinks SL
inner join #ExtraStructureLinks EL ON SL.SParentID = EL.SParentID AND SL.TParentID = EL.TParentID AND SL.SZ = EL.SZ

select SL.SID, MIN(PD.Distance) as NearestPartnerDistance into #NearestPartner from #StructureLinks SL
join #ExtraStructureLinks EL ON SL.SParentID = EL.SParentID AND SL.TParentID = EL.TParentID AND SL.SZ = EL.SZ
join #PairDistance PD ON PD.SID = SL.SID AND PD.TID = SL.TID
group by SL.SID
 
 */

/*
select SL.SID, SL.TID, PD.Distance into #PairsToDelete from #StructureLinks SL
inner join #PairDistance PD ON PD.SID = SL.SID AND PD.TID = SL.TID
inner join #NearestPartner NP ON NP.SID = SL.SID
where PD.Distance > NP.NearestPartnerDistance
*/

/*
--Remove the locations that have a nearer paired location
delete SL from #StructureLinks SL
inner join #PairDistance PD ON PD.SID = SL.SID AND PD.TID = SL.TID
inner join #NearestPartner NP ON NP.SID = SL.SID
where PD.Distance > NP.NearestPartnerDistance
*/
/*
--Validates we do not have extra pairs
select SParentID, TParentID, SZ, NumPairs from 
	(
		Select SParentID, TParentID, SZ, Count(SParentID) as NumPairs
		 from #StructureLinks 
		 group by SParentID, TParentID, SZ
	) as ExtraStructureLinks 
	
	where NumPairs > 1 order by NumPairs, SParentID

GO
*/

--Renders the first 1000 shapes in the spatial results view
IF OBJECT_ID('tempdb..#GAggregate') IS NOT NULL DROP TABLE #GAggregate
select SParentID as SParentID, SMosaicShape as Shape into #GAggregate from #StructureLinks where SMosaicShape is NOT NULL
insert into #GAggregate (SParentID, Shape) Select SParentID, TMosaicShape from #StructureLinks where TMosaicShape is NOT NULL
insert into #GAggregate (SParentID, Shape) Select SParentID, Line from #StructureLinks where Line IS NOT NULL

--select SParentID, Shape from #GAggregate where Shape is NULL OR Shape.STIsValid() = 0

--select TOP 1000 SParentID, Shape as Aggregate from #GAggregate order by SParentID
--select TOP 1000 SParentID, geometry::CollectionAggregate(Shape) as Aggregate from #GAggregate group by SParentID order by SParentID

GO

declare @AreaScalar float
--Measures the area of the PSD
set @AreaScalar = dbo.XYScale() * dbo.ZScale()
select S.ParentID as SourceParentID, SL.SParentID as SourceID, T.ParentID as TargetParentID, SL.TParentID as TargetID,
	   S.TypeID as SourceTypeID, T.TypeID as TargetTypeID, sum( Line.STLength()) * @AreaScalar as Area_nm,
	   sum( Line.STLength()) * @AreaScalar / 1000000.0 as Area_um--, geometry::CollectionAggregate(Shape) as shape 
from #StructureLinks SL
	join Structure S ON S.ID = SL.SParentID
	join Structure T ON T.ID = SL.TParentID
	group by SL.SParentID, SL.TParentID, S.ParentID, T.ParentID, S.TypeID, T.TypeID
	order by TargetTypeID