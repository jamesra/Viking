IF OBJECT_ID('tempdb..#OverlappingStructurePool') IS NOT NULL DROP TABLE #OverlappingStructurePool

select L1.ID as L1_ID, L2.ID as L2_ID, L1.ParentID as L1_ParentID, L2.ParentID as L2_ParentID,
	L1.VolumeShape as L1_VolumeShape, L2.VolumeShape as L2_VolumeShape, L1.Z as Z 
	into #OverlappingStructurePool from Location L1
	JOIN Structure S1 ON S1.ID = L1.ParentID
	JOIN Location L2 ON L2.Z = L1.Z AND L1.ParentID != L2.ParentID
	JOIN Structure S2 ON S2.ID = L2.ParentID
	WHERE L1.Z = 250 AND L1.ParentID != L2.ParentID AND
		  S1.ParentID IS NOT NULL AND S2.ParentID IS NOT NULL AND
		  L1.MosaicShape.STIntersects(L2.MosaicShape) = 1 AND L1.ID < L2.ID AND
		  NOT (S1.TypeID = 35 AND S2.TypeID = 35)
		  
delete SP from #OverlappingStructurePool SP
join StructureLink SL ON SL.SourceID = L1_ParentID AND SL.TargetID = L2_ParentID

delete SP from #OverlappingStructurePool SP
join StructureLink SL ON SL.SourceID = L2_ParentID AND SL.TargetID = L1_ParentID

select * from #OverlappingStructurePool