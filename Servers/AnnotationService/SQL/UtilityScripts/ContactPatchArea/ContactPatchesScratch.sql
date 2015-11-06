IF OBJECT_ID('tempdb..#StructureLinks') IS NOT NULL DROP TABLE #StructureLinks
IF OBJECT_ID('tempdb..#GAggregate') IS NOT NULL DROP TABLE #GAggregate

select S.ID as SID, S.ParentID as SParentID, S.Radius as SRadius, S.MosaicShape as SMosaicShape, S.Z as SZ,
	   T.ID as TID, T.ParentID as TParentID, T.Radius as TRadius, T.MosaicShape as TMosaicShape, T.Z as TZ ,
	    case 
			when S.MosaicShape.STDimension() = 2 then
				dbo.ufnLineFromIntersectingShapes(S.MosaicShape, T.MosaicShape)
	        when S.MosaicShape.STDimension() = 1 then
				S.MosaicShape
			ELSE
				NULL
			end
		as Line
INTO #StructureLinks
from Location S 
join StructureLink L ON L.SourceID = S.ParentID
join Location T      ON L.TargetID = T.ParentID AND T.Z = S.Z
where S.ParentID = 68181
order by S.ParentID, T.ParentID

select SMosaicShape as Shape into #GAggregate from #StructureLinks
insert into #GAggregate (Shape) Select TMosaicShape from #StructureLinks 
insert into #GAggregate (Shape) Select Line from #StructureLinks 

select geometry::CollectionAggregate(Shape) from #GAggregate

select SParentID, TParentID, sum( case when Points is not NULL and Points.STIsEmpty() = 0 then geometry::STLineFromText( 'LINESTRING ( ' + STR(Points.STGeometryN(1).STX) + ' ' +
												   STR(Points.STGeometryN(1).STY) + ', ' +
												   STR(Points.STGeometryN(2).STX) + ' ' +
												   STR(Points.STGeometryN(2).STY) + ')',0).STLength()
								  else
												   SRadius + TRadius
								  end
								) * 2.18 * 90 as Area_nm from #StructureLinks
												   group by SParentID, TParentID
												   order by Area_nm