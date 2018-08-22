
IF OBJECT_ID('tempdb..#StructureLinks') IS NOT NULL DROP TABLE #StructureLinks

select S.ID as SID, S.ParentID as SParentID, S.Radius as SRadius, S.VolumeShape as SVolumeShape, S.Z as SZ,
	   T.ID as TID, T.ParentID as TParentID, T.Radius as TRadius, T.VolumeShape as TVolumeShape, T.Z as TZ, 
	   case 
			when S.MosaicShape.STDimension() = 2 then
				S.MosaicShape.STBoundary().STIntersection(T.MosaicShape.STBoundary()) 
	        when S.MosaicShape.STDimension() = 1 then
				 S.MosaicShape
			ELSE
				NULL
			end
		as Points
INTO #StructureLinks
from Location S 
join StructureLink L ON L.SourceID = S.ParentID
join Location T      ON L.TargetID = T.ParentID
where T.Z = S.Z
order by S.ParentID, T.ParentID

select Points.ToString() from #StructureLinks

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