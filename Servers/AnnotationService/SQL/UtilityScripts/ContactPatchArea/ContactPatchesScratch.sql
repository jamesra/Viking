IF OBJECT_ID('tempdb..#StructureLinks') IS NOT NULL DROP TABLE #StructureLinks
IF OBJECT_ID('tempdb..#ExtraStructureLinks') IS NOT NULL DROP TABLE #ExtraStructureLinks
IF OBJECT_ID('tempdb..#ContactDuplicates') IS NOT NULL DROP TABLE #ContactDuplicates
IF OBJECT_ID('tempdb..#PairDistance') IS NOT NULL DROP TABLE #PairDistance
IF OBJECT_ID('tempdb..#NearestPartner') IS NOT NULL DROP TABLE #NearestPartner
IF OBJECT_ID('tempdb..#PairsToDelete') IS NOT NULL DROP TABLE #PairsToDelete

select S.ID as SID, S.ParentID as SParentID, S.Radius as SRadius, S.MosaicShape as SMosaicShape, S.Z as SZ ,
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
join Structure TargetStruct  ON TargetStruct.ID = T.ParentID 
where TargetStruct.ParentID = 606
order by S.ParentID, T.ParentID

GO 

declare @AreaScalar float
--Measures the area of the PSD
set @AreaScalar = dbo.XYScale() * dbo.ZScale()
select S.ParentID as SourceParentID, SP.Label as SourceLabel, SL.SParentID as SourceID,
	   T.ParentID as TargetParentID, TP.Label as TargetLabel, SL.TParentID as TargetID,
	   S.TypeID as SourceTypeID, T.TypeID as TargetTypeID, sum( Line.STLength()) * @AreaScalar as Area_nm,
	   sum( Line.STLength()) * @AreaScalar / 1000000.0 as Area_um--, geometry::CollectionAggregate(Shape) as shape 
from #StructureLinks SL
	join Structure S ON S.ID = SL.SParentID
	join Structure T ON T.ID = SL.TParentID
	join Structure SP ON SP.ID = S.ParentID
	join Structure TP ON TP.ID = T.ParentID
	group by SL.SParentID, SL.TParentID, S.ParentID, T.ParentID, S.TypeID, T.TypeID, SP.Label, TP.Label
	order by TargetTypeID